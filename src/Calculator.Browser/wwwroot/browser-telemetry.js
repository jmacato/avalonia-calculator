const snapshotIntervalMs = 1000;
const maximumPendingEvents = 48;

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
    let managedProbe = null;
    let managedProbeFailed = false;
    let managedProbeStarted = false;
    let managedProbeStartedAtMonoMs = 0;
    let managedSampleInFlight = false;
    let managedSampleAtMonoMs = 0;
    let managedSample = emptyManagedSample();
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
    });

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
            const transform = canvas.style.transform;
            if (transform !== canvasTransform) {
                canvasTransform = transform;
                canvasPresentTotal++;
                canvasPresentSinceLast++;
                lastCanvasPresentMonoMs = now;
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

    function requestManagedSample() {
        if (!managedProbe || !managedProbeStarted || managedProbeFailed || managedSampleInFlight) {
            return;
        }

        managedSampleInFlight = true;
        Promise.resolve(managedProbe.GetSnapshot())
            .then(encoded => {
                const values = String(encoded).split(',');
                managedSample = {
                    heapBytes: finiteNumber(values?.[0]),
                    allocatedBytes: finiteNumber(values?.[1]),
                    gen0: finiteNumber(values?.[2]),
                    gen1: finiteNumber(values?.[3]),
                    gen2: finiteNumber(values?.[4]),
                    dispatcherPulse: finiteNumber(values?.[5]),
                    dispatcherAgeMs: finiteNumber(values?.[6]),
                };
                managedSampleAtMonoMs = performance.now();
            })
            .catch(error => {
                managedProbeFailed = true;
                recordError('managed-probe-error', error);
            })
            .finally(() => {
                managedSampleInFlight = false;
            });
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
        const wasm = readWasmMemory();
        const managed = managedSample;
        const managedSampleAgeMs = managedSampleAtMonoMs > 0
            ? now - managedSampleAtMonoMs
            : managedProbeStarted ? now - managedProbeStartedAtMonoMs : -1;
        const managedDispatcherAgeMs = managed.dispatcherAgeMs >= 0 && managedSampleAtMonoMs > 0
            ? managed.dispatcherAgeMs + managedSampleAgeMs
            : -1;
        const jsHeapBytes = finiteNumber(performance.memory?.usedJSHeapSize);
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
            managedHeapBytes: managed.heapBytes,
            managedAllocatedBytes: managed.allocatedBytes,
            managedGen0Collections: managed.gen0,
            managedGen1Collections: managed.gen1,
            managedGen2Collections: managed.gen2,
            managedProbeStarted,
            managedProbeInFlight: managedSampleInFlight,
            managedSampleAgeMs,
            managedDispatcherPulse: managed.dispatcherPulse,
            managedDispatcherAgeMs,
        };

        worker.postMessage({ type: 'snapshot', snapshot, events });
        requestManagedSample();
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
    }, { capture: true, passive: true });
    window.addEventListener('pointermove', () => {
        pointerMoveTotal++;
        pointerMoveSinceLast++;
        lastPointerMonoMs = performance.now();
    }, { capture: true, passive: true });
    window.addEventListener('pointerup', event => {
        activePointers.delete(event.pointerId);
        pointerUpTotal++;
        lastPointerMonoMs = performance.now();
    }, { capture: true, passive: true });
    window.addEventListener('pointercancel', event => {
        activePointers.delete(event.pointerId);
        pointerCancelTotal++;
        lastPointerMonoMs = performance.now();
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
    window.addEventListener('wheel', () => {
        wheelTotal++;
        wheelSinceLast++;
    }, { capture: true, passive: true });
    window.addEventListener('keydown', () => { keyDownTotal++; }, true);
    window.addEventListener('beforeinput', () => { beforeInputTotal++; }, true);
    window.addEventListener('input', () => { inputTotal++; }, true);
    window.addEventListener('focusin', event => {
        focusInTotal++;
        queueEvent('focus-in', describeElement(event.target));
    }, true);
    window.addEventListener('focusout', event => {
        focusOutTotal++;
        queueEvent('focus-out', describeElement(event.target));
    }, true);
    window.addEventListener('contextmenu', event => {
        contextMenuTotal++;
        queueEvent('context-menu', describeElement(event.target));
    }, true);
    document.addEventListener('selectionchange', () => { selectionChangeTotal++; }, true);
    document.addEventListener('visibilitychange', () => queueEvent(`visibility-${document.visibilityState}`));
    window.addEventListener('focus', () => queueEvent('window-focus'));
    window.addEventListener('blur', () => queueEvent('window-blur'));
    window.addEventListener('online', () => queueEvent('online'));
    window.addEventListener('offline', () => queueEvent('offline'));
    window.addEventListener('pageshow', event => queueEvent('pageshow', `persisted=${event.persisted}`));
    window.addEventListener('pagehide', event => {
        queueEvent('pagehide', `persisted=${event.persisted}`);
        sendFinalBeacon('pagehide');
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
        async attachRuntime(dotnetRuntime, mainAssemblyName) {
            runtimeApi = dotnetRuntime;
            try {
                const exports = await dotnetRuntime.getAssemblyExports(mainAssemblyName);
                managedProbe = exports?.CalculatorApp?.Browser?.BrowserTelemetryExports ?? null;
                if (!managedProbe) {
                    queueEvent('managed-probe-unavailable');
                }
            } catch (error) {
                recordError('managed-probe-load-error', error);
            }
        },
        async startManagedProbe() {
            if (!managedProbe) {
                return;
            }
            try {
                await managedProbe.Start();
                managedProbeStarted = true;
                managedProbeStartedAtMonoMs = performance.now();
                queueEvent('managed-probe-started');
                requestManagedSample();
            } catch (error) {
                managedProbeFailed = true;
                recordError('managed-probe-start-error', error);
            }
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

function emptyManagedSample() {
    return {
        heapBytes: 0,
        allocatedBytes: 0,
        gen0: 0,
        gen1: 0,
        gen2: 0,
        dispatcherPulse: 0,
        dispatcherAgeMs: -1,
    };
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
