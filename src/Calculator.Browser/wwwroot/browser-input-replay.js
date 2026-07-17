const inputTraceStride = 16;
const kind = Object.freeze({
    pointerDown: 1,
    pointerMove: 2,
    pointerUp: 3,
    pointerCancel: 4,
    wheel: 5,
    keyDown: 6,
    keyUp: 7,
    beforeInput: 8,
    input: 9,
    focusIn: 10,
    focusOut: 11,
    resize: 12,
    visibility: 13,
    contextMenu: 14,
});

export async function replayBrowserInputSession(pageUrl, sessionId) {
    const endpoint = new URL(`/telemetry/v1/sessions/${encodeURIComponent(sessionId)}/inputs`, pageUrl);
    const response = await fetch(endpoint, { cache: 'no-store', credentials: 'same-origin' });
    if (!response.ok) {
        throw new Error(`Input trace request failed with status ${response.status}`);
    }

    const document = await response.json();
    const records = readRecords(document);
    const firstInputIndex = records.findIndex(isReplayableInput);
    if (firstInputIndex < 0) {
        return { eventCount: 0, durationMs: 0 };
    }

    const pointerTargets = new Map();
    const firstUptimeMs = records[firstInputIndex][0];
    const requestedSpeed = Number(pageUrl.searchParams.get('replay-speed'));
    const speed = Number.isFinite(requestedSpeed) && requestedSpeed >= 0.1 && requestedSpeed <= 16
        ? requestedSpeed
        : 1;
    const replayStartedAt = performance.now();
    let eventCount = 0;
    for (let index = firstInputIndex; index < records.length; index++) {
        const record = records[index];
        if (!isReplayableInput(record)) {
            continue;
        }

        const dueAt = replayStartedAt + (record[0] - firstUptimeMs) / speed;
        await waitUntil(dueAt);
        dispatchRecord(record, pointerTargets);
        eventCount++;
    }

    return {
        eventCount,
        durationMs: performance.now() - replayStartedAt,
        sourceDurationMs: records.at(-1)[0] - firstUptimeMs,
        speed,
    };
}

function readRecords(document) {
    if (!document || !Array.isArray(document.chunks)) {
        throw new Error('Input trace response has no chunks');
    }

    const records = [];
    let expectedSequence = null;
    for (const chunk of document.chunks) {
        if (chunk?.stride !== inputTraceStride || !Array.isArray(chunk.values) || chunk.values.length % inputTraceStride !== 0) {
            throw new Error('Input trace chunk has an invalid shape');
        }
        if (expectedSequence !== null && chunk.startSequence !== expectedSequence) {
            throw new Error(`Input trace sequence gap: expected ${expectedSequence}, received ${chunk.startSequence}`);
        }

        for (let offset = 0; offset < chunk.values.length; offset += inputTraceStride) {
            records.push(chunk.values.slice(offset, offset + inputTraceStride));
        }
        expectedSequence = chunk.startSequence + chunk.values.length / inputTraceStride;
    }
    return records;
}

function isReplayableInput(record) {
    return record[1] >= kind.pointerDown && record[1] <= kind.input || record[1] === kind.contextMenu;
}

function waitUntil(dueAt) {
    const delay = dueAt - performance.now();
    if (delay <= 0) {
        return Promise.resolve();
    }
    return new Promise(resolve => setTimeout(resolve, delay));
}

function dispatchRecord(record, pointerTargets) {
    const eventKind = record[1];
    if (eventKind >= kind.pointerDown && eventKind <= kind.pointerCancel) {
        dispatchPointer(record, pointerTargets);
        return;
    }
    if (eventKind === kind.wheel) {
        dispatchWheel(record);
        return;
    }
    if (eventKind === kind.keyDown || eventKind === kind.keyUp) {
        dispatchKey(record);
        return;
    }
    if (eventKind === kind.beforeInput || eventKind === kind.input) {
        dispatchTextInput(record);
        return;
    }
    if (eventKind === kind.contextMenu) {
        const point = scalePoint(record);
        targetAt(point).dispatchEvent(new MouseEvent('contextmenu', pointerOptions(record, point)));
    }
}

function dispatchPointer(record, pointerTargets) {
    const eventKind = record[1];
    const pointerId = record[3];
    const point = scalePoint(record);
    let target = pointerTargets.get(pointerId);
    if (eventKind === kind.pointerDown || !target?.isConnected) {
        target = targetAt(point);
        pointerTargets.set(pointerId, target);
    }

    const type = eventKind === kind.pointerDown
        ? 'pointerdown'
        : eventKind === kind.pointerMove
            ? 'pointermove'
            : eventKind === kind.pointerUp
                ? 'pointerup'
                : 'pointercancel';
    target.dispatchEvent(new PointerEvent(type, {
        ...pointerOptions(record, point),
        pointerId,
        pointerType: decodePointerType(record[2]),
        isPrimary: true,
        pressure: record[9],
    }));

    if (eventKind === kind.pointerUp || eventKind === kind.pointerCancel) {
        pointerTargets.delete(pointerId);
    }
}

function dispatchWheel(record) {
    const point = scalePoint(record);
    targetAt(point).dispatchEvent(new WheelEvent('wheel', {
        ...pointerOptions(record, point),
        deltaX: record[10],
        deltaY: record[11],
        deltaMode: record[14],
    }));
}

function dispatchKey(record) {
    const type = record[1] === kind.keyDown ? 'keydown' : 'keyup';
    const keyValue = decodeCharacter(record[14]);
    (document.activeElement ?? window).dispatchEvent(new KeyboardEvent(type, {
        bubbles: true,
        cancelable: true,
        composed: true,
        key: keyValue,
        altKey: (record[8] & 1) !== 0,
        ctrlKey: (record[8] & 2) !== 0,
        metaKey: (record[8] & 4) !== 0,
        shiftKey: (record[8] & 8) !== 0,
    }));
}

function dispatchTextInput(record) {
    const type = record[1] === kind.beforeInput ? 'beforeinput' : 'input';
    (document.activeElement ?? window).dispatchEvent(new InputEvent(type, {
        bubbles: true,
        cancelable: type === 'beforeinput',
        composed: true,
        data: decodeCharacter(record[14]),
        inputType: 'insertText',
    }));
}

function pointerOptions(record, point) {
    return {
        bubbles: true,
        cancelable: true,
        composed: true,
        clientX: point.x,
        clientY: point.y,
        button: record[6],
        buttons: record[7],
        altKey: (record[8] & 1) !== 0,
        ctrlKey: (record[8] & 2) !== 0,
        metaKey: (record[8] & 4) !== 0,
        shiftKey: (record[8] & 8) !== 0,
    };
}

function scalePoint(record) {
    return {
        x: record[4] * globalThis.innerWidth / Math.max(1, record[12]),
        y: record[5] * globalThis.innerHeight / Math.max(1, record[13]),
    };
}

function targetAt(point) {
    return document.elementFromPoint(point.x, point.y) ?? document.querySelector('#out canvas') ?? document.body;
}

function decodePointerType(value) {
    switch (value) {
        case 1: return 'touch';
        case 2: return 'pen';
        case 3: return 'mouse';
        default: return '';
    }
}

function decodeCharacter(value) {
    return value > 0 && value <= 0x10ffff ? String.fromCodePoint(value) : '';
}
