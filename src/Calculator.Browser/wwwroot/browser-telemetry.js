const snapshotIntervalMs = 1000;
const maximumPendingEvents = 48;
const dispatcherTelemetryChannelName = 'avalonia-browser-dispatcher-v1';
const inputTraceCapacity = 4096;
const inputTraceStride = 16;
const inputTraceKind = Object.freeze({
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

export function startBrowserTelemetry(pageUrl, cacheBustVersion) {
    const sessionId = createSessionId();
    const runId = cleanText(pageUrl.searchParams.get('run') || `device-${new Date().toISOString()}`, 96);
    const startedAtUnixMs = Date.now();
    const startedAtMonoMs = performance.now();
    const endpoint = new URL('/telemetry/v1/batch', pageUrl).href;
    const workerUrl = new URL('./telemetry-worker.js', import.meta.url);
    workerUrl.searchParams.set('v', cacheBustVersion);

    const worker = new Worker(workerUrl, { type: 'module', name: 'calculator-telemetry-probe' });
    const activePointers = new Set();
    let pendingEvents = [];
    let bootStage = 'telemetry-started';
    let uiSequence = 0;
    let runtimeApi = null;
    let canvas = null;
    let canvasTransform = '';
    let canvasDetectedAtMonoMs = 0;
    let lastCanvasPresentMonoMs = 0;
    let frameCountTotal = 0;
    let framesSinceLast = 0;
    let lastFrameMonoMs = performance.now();
    let maxFrameGapMs = 0;
    let maxFrameGapTotalMs = 0;
    let longFrameCountTotal = 0;
    let longTaskCountTotal = 0;
    let longTaskDurationSinceLastMs = 0;
    let maxLongTaskMs = 0;
    let pointerDownTotal = 0;
    let pointerMoveTotal = 0;
    let pointerMoveSinceLast = 0;
    let pointerUpTotal = 0;
    let pointerCancelTotal = 0;
    let wheelTotal = 0;
    let wheelSinceLast = 0;
    let lastPointerMonoMs = 0;
    let gotPointerCaptureTotal = 0;
    let lostPointerCaptureTotal = 0;
    let canvasPresentTotal = 0;
    let canvasPresentSinceLast = 0;
    let canvasFrameReceivedTotal = 0;
    let canvasFrameDroppedTotal = 0;
    let lastCanvasFrameReceived = 0;
    let lastCanvasFramePresented = 0;
    let lastCanvasFrameDropped = 0;
    let webGlContextLost = false;
    let webGlContextLosses = 0;
    let webGlContextRestores = 0;
    let keyDownTotal = 0;
    let beforeInputTotal = 0;
    let inputTotal = 0;
    let selectionChangeTotal = 0;
    let contextMenuTotal = 0;
    let focusInTotal = 0;
    let focusOutTotal = 0;
    let lastSnapshotMonoMs = performance.now();
    let dispatcherTelemetryChannel = null;
    let dispatcherRuntimeId = '';
    let managedProbeStarted = false;
    let managedDispatcherPulse = 0;
    let lastManagedDispatcherMonoMs = 0;
    let graphPipelineProbeStarted = false;
    let graphPipelineState = new Int32Array(19);
    const inputTrace = createInputTrace();

    const client = {
        startedAtUnixMs,
        userAgent: cleanText(navigator.userAgent, 384),
        platform: cleanText(navigator.platform, 96),
        language: cleanText(navigator.language, 48),
        hardwareConcurrency: finiteNumber(navigator.hardwareConcurrency),
        deviceMemoryGiB: finiteNumber(navigator.deviceMemory),
        screenWidth: finiteNumber(globalThis.screen?.width),
        screenHeight: finiteNumber(globalThis.screen?.height),
        screenPixelDepth: finiteNumber(globalThis.screen?.pixelDepth),
        crossOriginIsolated: globalThis.crossOriginIsolated === true,
        page: cleanText(`${pageUrl.pathname}${pageUrl.search}`, 384),
    };

    worker.postMessage({
        type: 'initialize',
        endpoint,
        sessionId,
        runId,
        client,
        intervalMs: snapshotIntervalMs,
        inputTrace: inputTrace?.descriptor ?? null,
    });

    try {
        dispatcherTelemetryChannel = new BroadcastChannel(dispatcherTelemetryChannelName);
        dispatcherTelemetryChannel.addEventListener('message', event => {
            const message = event.data;
            if (message?.source !== dispatcherTelemetryChannelName || typeof message.runtimeId !== 'string') {
                return;
            }

            if (message.kind === 'started') {
                dispatcherRuntimeId = message.runtimeId;
                queueEvent('managed-dispatcher-started', `runtime=${cleanText(dispatcherRuntimeId, 96)}`);
                return;
            }

            if (message.kind !== 'pulse' || message.runtimeId !== dispatcherRuntimeId) {
                return;
            }

            const sequence = finiteNumber(message.sequence);
            if (sequence <= managedDispatcherPulse) {
                return;
            }

            managedProbeStarted = true;
            managedDispatcherPulse = sequence;
            lastManagedDispatcherMonoMs = performance.now();
        });
    } catch {
        dispatcherTelemetryChannel = null;
    }

    function queueEvent(kind, detail = '') {
        if (pendingEvents.length >= maximumPendingEvents) {
            pendingEvents.shift();
        }

        pendingEvents.push({
            kind: cleanText(kind, 64),
            atUnixMs: Date.now(),
            uptimeMs: performance.now() - startedAtMonoMs,
            detail: cleanText(detail, 768),
        });
    }

    function mark(stage, detail = '') {
        const milestone = cleanText(stage, 80);
        queueEvent('milestone', detail ? `${milestone}: ${detail}` : milestone);
    }

    function recordError(kind, error) {
        queueEvent(kind, describeError(error));
    }

    function recordInput(kind, event, detail1 = 0, detail2 = 0) {
        if (!inputTrace) {
            return;
        }

        const pointerType = event instanceof PointerEvent
            ? encodePointerType(event.pointerType)
            : 0;
        const modifierBits = (event.altKey ? 1 : 0) |
            (event.ctrlKey ? 2 : 0) |
            (event.metaKey ? 4 : 0) |
            (event.shiftKey ? 8 : 0);
        inputTrace.write(
            performance.now() - startedAtMonoMs,
            kind,
            pointerType,
            finiteNumber(event.pointerId),
            finiteNumber(event.clientX),
            finiteNumber(event.clientY),
            finiteNumber(event.button),
            finiteNumber(event.buttons),
            modifierBits,
            finiteNumber(event.pressure),
            finiteNumber(event.deltaX),
            finiteNumber(event.deltaY),
            finiteNumber(globalThis.innerWidth),
            finiteNumber(globalThis.innerHeight),
            finiteNumber(detail1),
            finiteNumber(detail2));
    }

    function locateCanvas(now) {
        if (canvas?.isConnected) {
            return;
        }

        canvas = document.querySelector('#out > canvas.avalonia-canvas, #out canvas.avalonia-canvas');
        if (!canvas) {
            return;
        }

        canvasDetectedAtMonoMs = now;
        lastCanvasPresentMonoMs = now;
        canvasTransform = canvas.style.transform;
        lastCanvasFrameReceived = finiteNumber(canvas.__avaloniaFramesReceived);
        lastCanvasFramePresented = finiteNumber(canvas.__avaloniaFramesPresented);
        lastCanvasFrameDropped = finiteNumber(canvas.__avaloniaFramesDropped);
        canvasFrameReceivedTotal += lastCanvasFrameReceived;
        canvasPresentTotal += lastCanvasFramePresented;
        canvasFrameDroppedTotal += lastCanvasFrameDropped;
        canvas.addEventListener('webglcontextlost', event => {
            webGlContextLost = true;
            webGlContextLosses++;
            queueEvent('webgl-context-lost', `statusMessage=${cleanText(event.statusMessage, 256)}`);
        });
        canvas.addEventListener('webglcontextrestored', () => {
            webGlContextLost = false;
            webGlContextRestores++;
            queueEvent('webgl-context-restored');
        });
        queueEvent('canvas-detected', `${canvas.width}x${canvas.height}`);
    }

    function onAnimationFrame(now) {
        const gap = now - lastFrameMonoMs;
        lastFrameMonoMs = now;
        frameCountTotal++;
        framesSinceLast++;
        if (gap > maxFrameGapMs) {
            maxFrameGapMs = gap;
        }
        if (gap > maxFrameGapTotalMs) {
            maxFrameGapTotalMs = gap;
        }
        if (gap > 50 && document.visibilityState === 'visible') {
            longFrameCountTotal++;
        }

        if (!canvas?.isConnected && frameCountTotal % 30 === 0) {
            locateCanvas(now);
        } else if (canvas) {
            const received = finiteNumber(canvas.__avaloniaFramesReceived);
            const presented = finiteNumber(canvas.__avaloniaFramesPresented);
            const dropped = finiteNumber(canvas.__avaloniaFramesDropped);
            if (received > 0 || presented > 0 || dropped > 0) {
                const receivedDelta = received >= lastCanvasFrameReceived
                    ? received - lastCanvasFrameReceived
                    : received;
                const presentedDelta = presented >= lastCanvasFramePresented
                    ? presented - lastCanvasFramePresented
                    : presented;
                const droppedDelta = dropped >= lastCanvasFrameDropped
                    ? dropped - lastCanvasFrameDropped
                    : dropped;
                canvasFrameReceivedTotal += receivedDelta;
                canvasPresentTotal += presentedDelta;
                canvasPresentSinceLast += presentedDelta;
                canvasFrameDroppedTotal += droppedDelta;
                lastCanvasFrameReceived = received;
                lastCanvasFramePresented = presented;
                lastCanvasFrameDropped = dropped;
                if (presentedDelta > 0) {
                    lastCanvasPresentMonoMs = now;
                }
            } else {
                // Preserve telemetry compatibility for non-worker renderers.
                const transform = canvas.style.transform;
                if (transform !== canvasTransform) {
                    canvasTransform = transform;
                    canvasPresentTotal++;
                    canvasPresentSinceLast++;
                    lastCanvasPresentMonoMs = now;
                }
            }
        }

        requestAnimationFrame(onAnimationFrame);
    }

    function readWasmMemory() {
        try {
            const module = runtimeApi?.Module ?? globalThis.getDotnetRuntime?.(0)?.Module;
            const buffer = module?.HEAPU8?.buffer ?? module?.wasmMemory?.buffer;
            const bytes = finiteNumber(buffer?.byteLength);
            const reportedMaximum = finiteNumber(buffer?.maxByteLength);
            return {
                bytes,
                maximumBytes: buffer?.growable === true || reportedMaximum > bytes
                    ? reportedMaximum
                    : 0,
            };
        } catch {
            return { bytes: 0, maximumBytes: 0 };
        }
    }

    function refreshGraphPipeline() {
        const state = globalThis.calculatorGraphPipeline;
        if (typeof state !== 'string') {
            return;
        }

        const values = state.split(',');
        if (values.length < graphPipelineState.length) {
            return;
        }

        graphPipelineProbeStarted = true;
        for (let index = 0; index < graphPipelineState.length; index++) {
            graphPipelineState[index] = finiteNumber(values[index]);
        }
    }

    function createSnapshot() {
        const now = performance.now();
        locateCanvas(now);
        const intervalMs = Math.max(1, now - lastSnapshotMonoMs);
        const activeElement = document.activeElement;
        const nativeInput = activeElement instanceof HTMLInputElement && activeElement.classList.contains('avalonia-input-element')
            ? activeElement
            : null;
        const viewport = globalThis.visualViewport;
        const rect = canvas?.getBoundingClientRect();
        const inputHost = canvas?.parentElement;
        const wasm = readWasmMemory();
        const jsHeapBytes = finiteNumber(performance.memory?.usedJSHeapSize);
        const managedDispatcherAgeMs = lastManagedDispatcherMonoMs > 0
            ? now - lastManagedDispatcherMonoMs
            : 0;
        refreshGraphPipeline();
        const events = pendingEvents.length > 0 ? pendingEvents : null;
        pendingEvents = [];

        const snapshot = {
            sequence: ++uiSequence,
            capturedAtUnixMs: Date.now(),
            uptimeMs: now - startedAtMonoMs,
            bootStage,
            visibility: document.visibilityState,
            hasFocus: document.hasFocus(),
            frameCountTotal,
            framesSinceLast,
            frameRate: framesSinceLast * 1000 / intervalMs,
            maxFrameGapMs,
            maxFrameGapTotalMs,
            longFrameCountTotal,
            longTaskCountTotal,
            longTaskDurationSinceLastMs,
            maxLongTaskMs,
            pointerDownTotal,
            pointerMoveTotal,
            pointerMoveSinceLast,
            pointerUpTotal,
            pointerCancelTotal,
            wheelTotal,
            wheelSinceLast,
            activePointers: activePointers.size,
            lastPointerAgeMs: lastPointerMonoMs > 0 ? now - lastPointerMonoMs : -1,
            gotPointerCaptureTotal,
            lostPointerCaptureTotal,
            canvasFound: canvas !== null,
            canvasWidth: finiteNumber(canvas?.width),
            canvasHeight: finiteNumber(canvas?.height),
            canvasCssWidth: finiteNumber(rect?.width),
            canvasCssHeight: finiteNumber(rect?.height),
            canvasPresentTotal,
            canvasPresentSinceLast,
            canvasPresentAgeMs: lastCanvasPresentMonoMs > 0 ? now - lastCanvasPresentMonoMs : -1,
            canvasFrameReceivedTotal,
            canvasFramePresentedTotal: canvasPresentTotal,
            canvasFrameDroppedTotal,
            canvasFramePending: Math.max(0,
                lastCanvasFrameReceived - lastCanvasFramePresented - lastCanvasFrameDropped),
            inputQueueDepth: finiteNumber(inputHost?.__avaloniaInputQueueDepth),
            inputQueueHighWater: finiteNumber(inputHost?.__avaloniaInputQueueHighWater),
            inputQueueDequeued: finiteNumber(inputHost?.__avaloniaInputQueueDequeued),
            inputQueueRetries: finiteNumber(inputHost?.__avaloniaInputQueueRetries),
            inputQueueShed: finiteNumber(inputHost?.__avaloniaInputQueueShed),
            webGlContextLost,
            webGlContextLosses,
            webGlContextRestores,
            keyDownTotal,
            beforeInputTotal,
            inputTotal,
            selectionChangeTotal,
            contextMenuTotal,
            focusInTotal,
            focusOutTotal,
            activeElement: describeElement(activeElement),
            nativeInputFocused: nativeInput !== null,
            nativeInputValueLength: nativeInput?.value.length ?? 0,
            nativeSelectionStart: nativeInput?.selectionStart ?? -1,
            nativeSelectionEnd: nativeInput?.selectionEnd ?? -1,
            visualViewportWidth: finiteNumber(viewport?.width),
            visualViewportHeight: finiteNumber(viewport?.height),
            visualViewportScale: finiteNumber(viewport?.scale),
            visualViewportOffsetTop: finiteNumber(viewport?.offsetTop),
            innerWidth: globalThis.innerWidth,
            innerHeight: globalThis.innerHeight,
            devicePixelRatio: finiteNumber(globalThis.devicePixelRatio),
            wasmMemoryBytes: wasm.bytes,
            wasmMemoryMaxBytes: wasm.maximumBytes,
            jsHeapBytes,
            managedProbeStarted,
            managedProbeInFlight: false,
            managedSampleAgeMs: managedDispatcherAgeMs,
            managedDispatcherPulse,
            managedDispatcherAgeMs,
            graphPipelineProbeStarted,
            graphPipelineProbeInFlight: false,
            graphRequestedGeneration: finiteNumber(graphPipelineState[0]),
            graphWorkerGeneration: finiteNumber(graphPipelineState[1]),
            graphCompletedGeneration: finiteNumber(graphPipelineState[2]),
            graphPublishedGeneration: finiteNumber(graphPipelineState[3]),
            graphCommittedGeneration: finiteNumber(graphPipelineState[4]),
            graphWorkerActive: finiteNumber(graphPipelineState[5]),
            graphCompletedStatus: finiteNumber(graphPipelineState[6]),
            graphCommitStatus: finiteNumber(graphPipelineState[7]),
            graphRequestCount: finiteNumber(graphPipelineState[8]),
            graphWorkerStartCount: finiteNumber(graphPipelineState[9]),
            graphWorkerCompletionCount: finiteNumber(graphPipelineState[10]),
            graphCommitCount: finiteNumber(graphPipelineState[11]),
            graphCommitMissCount: finiteNumber(graphPipelineState[12]),
            graphSettlementTimerCount: finiteNumber(graphPipelineState[13]),
            graphSettlementRequestCount: finiteNumber(graphPipelineState[14]),
            graphRenderCount: finiteNumber(graphPipelineState[15]),
            graphRendererActiveCount: finiteNumber(graphPipelineState[16]),
            graphRendererCreatedCount: finiteNumber(graphPipelineState[17]),
            graphRendererDisposedCount: finiteNumber(graphPipelineState[18]),
        };

        worker.postMessage({ type: 'snapshot', snapshot, events });
        framesSinceLast = 0;
        maxFrameGapMs = 0;
        longTaskDurationSinceLastMs = 0;
        pointerMoveSinceLast = 0;
        wheelSinceLast = 0;
        canvasPresentSinceLast = 0;
        lastSnapshotMonoMs = now;
        return { snapshot, events };
    }

    function sendFinalBeacon(reason) {
        const { snapshot, events } = createSnapshot();
        const payload = JSON.stringify({
            version: 1,
            sessionId,
            runId,
            source: 'ui-beacon',
            workerSequence: 0,
            sentAtUnixMs: Date.now(),
            workerUptimeMs: performance.now() - startedAtMonoMs,
            workerUploadFailures: 0,
            skippedUploads: 0,
            uiAgeMs: 0,
            client,
            ui: snapshot,
            events: events ?? [{
                kind: reason,
                atUnixMs: Date.now(),
                uptimeMs: performance.now() - startedAtMonoMs,
                detail: '',
            }],
        });
        navigator.sendBeacon?.(endpoint, new Blob([payload], { type: 'application/json' }));
        worker.postMessage({ type: 'flush' });
    }

    window.addEventListener('pointerdown', event => {
        activePointers.add(event.pointerId);
        pointerDownTotal++;
        lastPointerMonoMs = performance.now();
        recordInput(inputTraceKind.pointerDown, event, event.offsetX, event.offsetY);
    }, { capture: true, passive: true });
    window.addEventListener('pointermove', event => {
        pointerMoveTotal++;
        pointerMoveSinceLast++;
        lastPointerMonoMs = performance.now();
        recordInput(inputTraceKind.pointerMove, event, event.offsetX, event.offsetY);
    }, { capture: true, passive: true });
    window.addEventListener('pointerup', event => {
        activePointers.delete(event.pointerId);
        pointerUpTotal++;
        lastPointerMonoMs = performance.now();
        recordInput(inputTraceKind.pointerUp, event, event.offsetX, event.offsetY);
    }, { capture: true, passive: true });
    window.addEventListener('pointercancel', event => {
        activePointers.delete(event.pointerId);
        pointerCancelTotal++;
        lastPointerMonoMs = performance.now();
        recordInput(inputTraceKind.pointerCancel, event, event.offsetX, event.offsetY);
        queueEvent('pointer-cancel', `pointerId=${event.pointerId}`);
    }, { capture: true, passive: true });
    window.addEventListener('gotpointercapture', event => {
        gotPointerCaptureTotal++;
        queueEvent('got-pointer-capture', `pointerId=${event.pointerId}; target=${describeElement(event.target)}`);
    }, true);
    window.addEventListener('lostpointercapture', event => {
        lostPointerCaptureTotal++;
        queueEvent('lost-pointer-capture', `pointerId=${event.pointerId}; target=${describeElement(event.target)}`);
    }, true);
    window.addEventListener('wheel', event => {
        wheelTotal++;
        wheelSinceLast++;
        recordInput(inputTraceKind.wheel, event, event.deltaMode);
    }, { capture: true, passive: true });
    window.addEventListener('keydown', event => {
        keyDownTotal++;
        recordInput(inputTraceKind.keyDown, event, encodeKey(event.key), encodeText(event.code));
    }, true);
    window.addEventListener('keyup', event => {
        recordInput(inputTraceKind.keyUp, event, encodeKey(event.key), encodeText(event.code));
    }, true);
    window.addEventListener('beforeinput', event => {
        beforeInputTotal++;
        recordInput(inputTraceKind.beforeInput, event, encodeKey(event.data), encodeText(event.inputType));
    }, true);
    window.addEventListener('input', event => {
        inputTotal++;
        recordInput(inputTraceKind.input, event, encodeKey(event.data), encodeText(event.inputType));
    }, true);
    window.addEventListener('focusin', event => {
        focusInTotal++;
        recordInput(inputTraceKind.focusIn, event, encodeText(describeElement(event.target)));
        queueEvent('focus-in', describeElement(event.target));
    }, true);
    window.addEventListener('focusout', event => {
        focusOutTotal++;
        recordInput(inputTraceKind.focusOut, event, encodeText(describeElement(event.target)));
        queueEvent('focus-out', describeElement(event.target));
    }, true);
    window.addEventListener('contextmenu', event => {
        contextMenuTotal++;
        recordInput(inputTraceKind.contextMenu, event);
        queueEvent('context-menu', describeElement(event.target));
    }, true);
    document.addEventListener('selectionchange', () => { selectionChangeTotal++; }, true);
    document.addEventListener('visibilitychange', event => {
        recordInput(inputTraceKind.visibility, event, encodeText(document.visibilityState));
        queueEvent(`visibility-${document.visibilityState}`);
    });
    window.addEventListener('resize', event => recordInput(inputTraceKind.resize, event), { passive: true });
    window.addEventListener('focus', () => queueEvent('window-focus'));
    window.addEventListener('blur', () => queueEvent('window-blur'));
    window.addEventListener('online', () => queueEvent('online'));
    window.addEventListener('offline', () => queueEvent('offline'));
    window.addEventListener('pageshow', event => queueEvent('pageshow', `persisted=${event.persisted}`));
    window.addEventListener('pagehide', event => {
        queueEvent('pagehide', `persisted=${event.persisted}`);
        sendFinalBeacon('pagehide');
        dispatcherTelemetryChannel?.close();
    });
    document.addEventListener('freeze', () => {
        queueEvent('freeze');
        sendFinalBeacon('freeze');
    });
    document.addEventListener('resume', () => queueEvent('resume'));
    window.addEventListener('error', event => {
        if (event.error) {
            recordError('error', event.error);
        } else {
            queueEvent('resource-error', cleanText(event.target?.src || event.target?.href || event.message, 768));
        }
    }, true);
    window.addEventListener('unhandledrejection', event => recordError('unhandled-rejection', event.reason));
    worker.addEventListener('error', event => queueEvent('worker-error', `${event.message} @ ${event.filename}:${event.lineno}:${event.colno}`));
    worker.addEventListener('messageerror', () => queueEvent('worker-message-error'));

    try {
        if (globalThis.PerformanceObserver?.supportedEntryTypes?.includes('longtask')) {
            const observer = new PerformanceObserver(list => {
                for (const entry of list.getEntries()) {
                    longTaskCountTotal++;
                    longTaskDurationSinceLastMs += entry.duration;
                    if (entry.duration > maxLongTaskMs) {
                        maxLongTaskMs = entry.duration;
                    }
                }
            });
            observer.observe({ type: 'longtask', buffered: true });
        }
    } catch {
        // Event-loop gaps remain available where Long Tasks is unsupported.
    }

    requestAnimationFrame(onAnimationFrame);
    setInterval(createSnapshot, snapshotIntervalMs);
    queueEvent('telemetry-start', `session=${sessionId}`);
    createSnapshot();

    return {
        sessionId,
        runId,
        mark,
        recordError,
        setBootStage(stage) {
            bootStage = cleanText(stage, 80);
        },
        attachRuntime(dotnetRuntime) {
            runtimeApi = dotnetRuntime;
        },
    };
}

function createSessionId() {
    if (typeof crypto.randomUUID === 'function') {
        return crypto.randomUUID();
    }

    const values = new Uint32Array(4);
    crypto.getRandomValues(values);
    return Array.from(values, value => value.toString(16).padStart(8, '0')).join('-');
}

function finiteNumber(value) {
    const number = Number(value);
    return Number.isFinite(number) ? number : 0;
}

function createInputTrace() {
    if (typeof SharedArrayBuffer !== 'function') {
        return null;
    }

    try {
        const controlBuffer = new SharedArrayBuffer(Int32Array.BYTES_PER_ELEMENT * 2);
        const slotSequenceBuffer = new SharedArrayBuffer(Int32Array.BYTES_PER_ELEMENT * inputTraceCapacity);
        const valuesBuffer = new SharedArrayBuffer(Float64Array.BYTES_PER_ELEMENT * inputTraceCapacity * inputTraceStride);
        const control = new Int32Array(controlBuffer);
        const slotSequences = new Int32Array(slotSequenceBuffer);
        const values = new Float64Array(valuesBuffer);
        return {
            descriptor: {
                capacity: inputTraceCapacity,
                stride: inputTraceStride,
                controlBuffer,
                slotSequenceBuffer,
                valuesBuffer,
            },
            write(uptimeMs, kind, pointerType, pointerId, clientX, clientY, button, buttons, modifiers, pressure, deltaX, deltaY, viewportWidth, viewportHeight, detail1, detail2) {
                const sequence = Atomics.add(control, 0, 1) + 1;
                const slot = (sequence - 1) % inputTraceCapacity;
                const offset = slot * inputTraceStride;
                values[offset] = uptimeMs;
                values[offset + 1] = kind;
                values[offset + 2] = pointerType;
                values[offset + 3] = pointerId;
                values[offset + 4] = clientX;
                values[offset + 5] = clientY;
                values[offset + 6] = button;
                values[offset + 7] = buttons;
                values[offset + 8] = modifiers;
                values[offset + 9] = pressure;
                values[offset + 10] = deltaX;
                values[offset + 11] = deltaY;
                values[offset + 12] = viewportWidth;
                values[offset + 13] = viewportHeight;
                values[offset + 14] = detail1;
                values[offset + 15] = detail2;
                Atomics.store(slotSequences, slot, sequence);
                Atomics.store(control, 1, sequence);
            },
        };
    } catch {
        return null;
    }
}

function encodePointerType(pointerType) {
    switch (pointerType) {
        case 'touch': return 1;
        case 'pen': return 2;
        case 'mouse': return 3;
        default: return 0;
    }
}

function encodeKey(value) {
    if (typeof value !== 'string' || value.length === 0) {
        return 0;
    }

    const codePoint = value.codePointAt(0) ?? 0;
    return value.length === String.fromCodePoint(codePoint).length
        ? codePoint
        : -encodeText(value);
}

function encodeText(value) {
    if (typeof value !== 'string' || value.length === 0) {
        return 0;
    }

    let hash = 2166136261;
    for (let index = 0; index < value.length; index++) {
        hash ^= value.charCodeAt(index);
        hash = Math.imul(hash, 16777619);
    }
    return hash >>> 0;
}


function cleanText(value, maximumLength) {
    const text = value == null ? '' : String(value);
    return text.length <= maximumLength ? text : text.slice(0, maximumLength);
}

function describeError(error) {
    if (error instanceof Error) {
        return cleanText(`${error.name}: ${error.message}${error.stack ? `\n${error.stack}` : ''}`, 768);
    }

    return cleanText(error, 768);
}

function describeElement(value) {
    if (!(value instanceof Element)) {
        return value === document ? 'document' : value === window ? 'window' : 'none';
    }

    const id = value.id ? `#${value.id}` : '';
    const classes = value.classList.length > 0 ? `.${Array.from(value.classList).slice(0, 4).join('.')}` : '';
    return cleanText(`${value.tagName.toLowerCase()}${id}${classes}`, 192);
}
