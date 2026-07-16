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

self.onmessage = event => {
    const message = event.data;
    if (message?.type === 'initialize') {
        endpoint = message.endpoint;
        sessionId = message.sessionId;
        runId = message.runId;
        client = message.client;
        intervalMs = Math.max(500, Number(message.intervalMs) || 1000);
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
    } catch {
        uploadFailures++;
    } finally {
        clearTimeout(timeout);
        inFlight = false;
    }
}
