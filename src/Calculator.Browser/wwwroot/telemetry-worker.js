let initialized = false;
let endpoint = '';
let sessionId = '';
let runId = '';
let client = null;
let intervalMs = 1000;
let startedAtMonoMs = performance.now();
let workerSequence = 0;
let uploadFailures = 0;
let skippedUploads = 0;
let inFlight = false;
let lastUiSnapshot = null;
let lastUiSnapshotAtMonoMs = 0;
let pendingEvents = [];
let timer = 0;
let inputTrace = null;
let inputTraceCursor = 0;
let inputTraceDropped = 0;
const maximumInputTraceRecordsPerBatch = 256;

self.onmessage = event => {
    const message = event.data;
    if (message?.type === 'initialize') {
        endpoint = message.endpoint;
        sessionId = message.sessionId;
        runId = message.runId;
        client = message.client;
        intervalMs = Math.max(500, Number(message.intervalMs) || 1000);
        inputTrace = openInputTrace(message.inputTrace);
        startedAtMonoMs = performance.now();
        initialized = true;
        if (timer) {
            clearInterval(timer);
        }
        timer = setInterval(() => { void upload(); }, intervalMs);
        void upload();
        return;
    }

    if (message?.type === 'snapshot') {
        lastUiSnapshot = message.snapshot;
        lastUiSnapshotAtMonoMs = performance.now();
        if (Array.isArray(message.events) && message.events.length > 0) {
            pendingEvents.push(...message.events);
            if (pendingEvents.length > 64) {
                pendingEvents.splice(0, pendingEvents.length - 64);
            }
        }
        return;
    }

    if (message?.type === 'flush') {
        void upload();
    }
};

async function upload() {
    if (!initialized) {
        return;
    }
    if (inFlight) {
        skippedUploads++;
        return;
    }

    inFlight = true;
    const now = performance.now();
    const eventCount = pendingEvents.length;
    const inputBatch = readInputTraceBatch();
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), Math.max(3000, intervalMs * 3));
    const batch = {
        version: 1,
        sessionId,
        runId,
        source: 'worker',
        workerSequence: ++workerSequence,
        sentAtUnixMs: Date.now(),
        workerUptimeMs: now - startedAtMonoMs,
        workerUploadFailures: uploadFailures,
        skippedUploads,
        uiAgeMs: lastUiSnapshotAtMonoMs > 0 ? now - lastUiSnapshotAtMonoMs : -1,
        client,
        ui: lastUiSnapshot,
        events: eventCount > 0 ? pendingEvents.slice(0, eventCount) : null,
        inputTraceStartSequence: inputBatch?.startSequence ?? 0,
        inputTraceStride: inputBatch?.stride ?? 0,
        inputTraceDropped: inputBatch?.dropped ?? inputTraceDropped,
        inputTraceValues: inputBatch?.values ?? null,
    };

    try {
        const response = await fetch(endpoint, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(batch),
            cache: 'no-store',
            credentials: 'same-origin',
            signal: controller.signal,
        });
        if (!response.ok) {
            throw new Error(`Telemetry server returned ${response.status}`);
        }

        if (eventCount > 0) {
            pendingEvents.splice(0, eventCount);
        }
        if (inputBatch) {
            inputTraceCursor = inputBatch.endSequence;
            inputTraceDropped = inputBatch.dropped;
        }
    } catch {
        uploadFailures++;
    } finally {
        clearTimeout(timeout);
        inFlight = false;
    }
}

function openInputTrace(descriptor) {
    if (!descriptor ||
        !(descriptor.controlBuffer instanceof SharedArrayBuffer) ||
        !(descriptor.slotSequenceBuffer instanceof SharedArrayBuffer) ||
        !(descriptor.valuesBuffer instanceof SharedArrayBuffer)) {
        return null;
    }

    const capacity = Math.trunc(Number(descriptor.capacity));
    const stride = Math.trunc(Number(descriptor.stride));
    if (capacity <= 0 || stride <= 0) {
        return null;
    }

    return {
        capacity,
        stride,
        control: new Int32Array(descriptor.controlBuffer),
        slotSequences: new Int32Array(descriptor.slotSequenceBuffer),
        values: new Float64Array(descriptor.valuesBuffer),
    };
}

function readInputTraceBatch() {
    if (!inputTrace) {
        return null;
    }

    const latestSequence = Atomics.load(inputTrace.control, 1);
    if (latestSequence <= inputTraceCursor) {
        return null;
    }

    const oldestAvailable = Math.max(1, latestSequence - inputTrace.capacity + 1);
    const startSequence = Math.max(inputTraceCursor + 1, oldestAvailable);
    const dropped = inputTraceDropped + Math.max(0, startSequence - inputTraceCursor - 1);

    const endSequence = Math.min(latestSequence, startSequence + maximumInputTraceRecordsPerBatch - 1);
    const values = new Array((endSequence - startSequence + 1) * inputTrace.stride);
    let destination = 0;
    let actualEndSequence = startSequence - 1;
    for (let sequence = startSequence; sequence <= endSequence; sequence++) {
        const slot = (sequence - 1) % inputTrace.capacity;
        if (Atomics.load(inputTrace.slotSequences, slot) !== sequence) {
            break;
        }

        const offset = slot * inputTrace.stride;
        const recordStart = destination;
        for (let index = 0; index < inputTrace.stride; index++) {
            values[destination++] = inputTrace.values[offset + index];
        }
        if (Atomics.load(inputTrace.slotSequences, slot) !== sequence) {
            destination = recordStart;
            break;
        }
        actualEndSequence = sequence;
    }

    if (actualEndSequence < startSequence) {
        return null;
    }

    values.length = destination;
    return {
        startSequence,
        endSequence: actualEndSequence,
        stride: inputTrace.stride,
        dropped,
        values,
    };
}
