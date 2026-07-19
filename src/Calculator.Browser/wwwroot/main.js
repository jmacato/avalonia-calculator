const isBrowser = typeof window !== 'undefined';
const cacheBustVersion = new URL(import.meta.url).searchParams.get('v') ?? `${Date.now()}`;
const pageUrl = new URL(globalThis.location.href);
// .NET's five-worker default leaves too little reserve once its own runtime,
// Avalonia's UI dispatcher, and the compositor are active. Six keeps three
// workers preloaded after those startup threads are running without forcing
// WebKit to instantiate eight copies of the large AOT module at once.
const pthreadPoolInitialSize = 6;
const pthreadPoolUnusedSize = 2;
const initialWasmMemoryBytes = 96 * 1024 * 1024;
const aotProfileDelaySeconds = Number(pageUrl.searchParams.get('collect-aot-profile'));
const collectAotProfile = Number.isFinite(aotProfileDelaySeconds) && aotProfileDelaySeconds > 0;
const inputReplaySessionId = pageUrl.searchParams.get('replay-input');
let browserTelemetry = null;
let inputReplayScheduled = false;
let fatalErrorVisible = false;
let runtimeHealthMonitorId = 0;

if (!isBrowser) {
    throw new Error('Expected to run in a browser');
}

const fatalError = {
    root: document.getElementById('fatal-error'),
    title: document.querySelector('.fatal-error-title'),
    message: document.querySelector('.fatal-error-message'),
    diagnosticId: document.querySelector('.fatal-error-diagnostic-id'),
    reload: document.querySelector('.fatal-error-reload'),
};

if (pageUrl.searchParams.get('telemetry') === '1') {
    try {
        const telemetryModule = await import(`./browser-telemetry.js?v=${encodeURIComponent(cacheBustVersion)}`);
        browserTelemetry = telemetryModule.startBrowserTelemetry(pageUrl, cacheBustVersion);
    } catch (error) {
        console.error('Browser telemetry failed to start:', error);
    }
}

const loadingProgress = {
    root: document.getElementById('splash'),
    bar: document.querySelector('.splash-progress'),
    fill: document.querySelector('.splash-progress-fill'),
    status: document.querySelector('.splash-status'),
    detail: document.querySelector('.splash-progress-detail'),
};
let displayedProgress = 0;

function describeError(error) {
    if (error instanceof Error) {
        return `${error.name}: ${error.message}${error.stack ? `\n${error.stack}` : ''}`;
    }

    if (error && (typeof error === 'object' || typeof error === 'function')) {
        const isWebAssemblyException = typeof WebAssembly.Exception === 'function' &&
            error instanceof WebAssembly.Exception;
        const name = isWebAssemblyException
            ? 'WebAssembly.Exception'
            : typeof error.name === 'string' && error.name
                ? error.name
                : typeof error.constructor?.name === 'string' && error.constructor.name
                    ? error.constructor.name
                    : 'Error';
        const message = typeof error.message === 'string' ? error.message : '';
        const stack = typeof error.stack === 'string' ? error.stack : '';
        const location = typeof error.filename === 'string' && error.filename
            ? `${error.filename}${Number.isFinite(error.lineno) ? `:${error.lineno}` : ''}`
            : '';
        const nested = error.error && error.error !== error
            ? describeError(error.error)
            : '';
        const details = [message, location, stack, nested].filter(Boolean).join('\n');
        return details ? `${name}: ${details}` : name;
    }

    return String(error);
}

function hashDiagnosticText(text) {
    let hash = 2166136261;
    for (let index = 0; index < text.length; index++) {
        hash ^= text.charCodeAt(index);
        hash = Math.imul(hash, 16777619);
    }

    return (hash >>> 0).toString(16).padStart(8, '0').toUpperCase();
}

function formatUnsignedHex(value, width) {
    return (Number(value) >>> 0).toString(16).padStart(width, '0').toUpperCase();
}

function createDiagnosticId(source, description, nativeState) {
    const timestamp = Date.now().toString(36).toUpperCase();
    const sourceCode = source.replace(/[^a-z0-9]/gi, '').slice(0, 5).toUpperCase() || 'ERROR';
    const signature = nativeState
        ? `${formatUnsignedHex(nativeState.stage, 2)}${formatUnsignedHex(nativeState.exceptionType, 2)}${formatUnsignedHex(nativeState.hResult, 8)}`
        : hashDiagnosticText(description);

    return `CALC-${timestamp}-${sourceCode}-${signature}`;
}

function showFatalError(error, source, nativeState = null) {
    if (fatalErrorVisible) {
        return;
    }

    const description = describeError(error);
    const diagnosticId = createDiagnosticId(source, description, nativeState);
    const isStartupFailure = source === 'boot' || source === 'startup' || source === 'wasm-abort';
    fatalErrorVisible = true;

    if (runtimeHealthMonitorId !== 0) {
        clearInterval(runtimeHealthMonitorId);
        runtimeHealthMonitorId = 0;
    }

    loadingProgress.root?.remove();
    document.body.classList.add('runtime-failed');

    if (fatalError.title) {
        fatalError.title.textContent = isStartupFailure
            ? 'Calculator could not start'
            : 'Calculator stopped';
    }

    if (fatalError.message) {
        fatalError.message.textContent = isStartupFailure
            ? 'The application could not finish loading. Reload it to try again.'
            : 'A fatal runtime error stopped the application. Reload it to continue.';
    }

    if (fatalError.diagnosticId) {
        fatalError.diagnosticId.textContent = diagnosticId;
    }

    if (fatalError.root) {
        fatalError.root.hidden = false;
    }

    requestAnimationFrame(() => fatalError.reload?.focus({ preventScroll: true }));

    browserTelemetry?.setBootStage('fatal-error');
    browserTelemetry?.mark('fatal-error', `id=${diagnosticId}; source=${source}`);
    browserTelemetry?.recordError(`fatal-${source}`, error);
    console.error(`Calculator fatal error [${diagnosticId}] (${source}):`, error);
}

function isFatalRuntimeError(error) {
    const description = describeError(error);
    const isWebAssemblyRuntimeError = typeof WebAssembly.RuntimeError === 'function' &&
        error instanceof WebAssembly.RuntimeError;
    const isWebAssemblyException = typeof WebAssembly.Exception === 'function' &&
        error instanceof WebAssembly.Exception;
    return isWebAssemblyRuntimeError || isWebAssemblyException ||
        /(?:indirect call|function) signature mismatch|out of bounds memory access|memory access out of bounds|mono[^\n]*assert|runtime[^\n]*(?:abort|terminated)|wasm[^\n]*(?:exception|trap)/i.test(description);
}

function startRuntimeHealthMonitor(dotnetRuntime) {
    let lastDispatcherStage = 0;
    let lastDispatcherStageSequence = 0;
    let lastDispatcherProgressAt = performance.now();

    const inspectRuntime = () => {
        if (fatalErrorVisible) {
            return;
        }

        try {
            const module = dotnetRuntime?.Module ?? globalThis.getDotnetRuntime?.(0)?.Module;
            const getField = module?._avalonia_browser_dispatcher_debug_get;
            if (typeof getField === 'function') {
                const dispatcherStage = getField(1) >>> 0;
                const dispatcherStageSequence = getField(2) >>> 0;
                if (dispatcherStage !== lastDispatcherStage ||
                    dispatcherStageSequence !== lastDispatcherStageSequence) {
                    lastDispatcherStage = dispatcherStage;
                    lastDispatcherStageSequence = dispatcherStageSequence;
                    lastDispatcherProgressAt = performance.now();
                } else if (pageUrl.searchParams.get('runtime-diagnostics') === '1' &&
                    (dispatcherStage === 10 || dispatcherStage === 20 ||
                        dispatcherStage === 30 || dispatcherStage === 40) &&
                    performance.now() - lastDispatcherProgressAt >= 1_000) {
                    browserTelemetry?.mark(
                        'dispatcher-stalled',
                        `stage=${dispatcherStage}; sequence=${dispatcherStageSequence}`);
                    lastDispatcherProgressAt = performance.now();
                }

                const loopExitCount = getField(16) >>> 0;
                if (loopExitCount !== 0) {
                    const nativeState = {
                        stage: dispatcherStage,
                        hResult: getField(14) >>> 0,
                        exceptionType: getField(15) >>> 0,
                        loopExitCount,
                    };
                    const error = new Error(
                        `Managed UI dispatcher exited at stage ${nativeState.stage}; ` +
                        `exception type ${nativeState.exceptionType}; ` +
                        `HRESULT 0x${formatUnsignedHex(nativeState.hResult, 8)}.`);
                    showFatalError(error, 'dispatcher', nativeState);
                    return;
                }
            }
        } catch (error) {
            showFatalError(error, 'health-monitor');
            return;
        }
    };

    runtimeHealthMonitorId = setInterval(inspectRuntime, 250);
    inspectRuntime();
}

fatalError.reload?.addEventListener('click', () => {
    const reloadUrl = new URL(globalThis.location.href);
    reloadUrl.searchParams.set('cache-bust', `${Date.now()}`);
    globalThis.location.replace(reloadUrl.href);
});
globalThis.addEventListener('error', event => {
    const error = event.error ?? event;
    if (isFatalRuntimeError(error)) {
        showFatalError(error, 'runtime');
    }
});
globalThis.addEventListener('unhandledrejection', event => {
    if (isFatalRuntimeError(event.reason)) {
        showFatalError(event.reason, 'runtime-promise');
    }
});

function loadingFailureStatus(error) {
    const text = describeError(error);
    if (/mono_download_assets|download .* failed|load failed|fetch|network/i.test(text)) {
        return 'Runtime asset download failed';
    }

    return 'Load failed';
}

function setLoadingProgress(value, status, detail = '') {
    displayedProgress = Math.max(displayedProgress, Math.min(1, Math.max(0, value)));
    const percent = displayedProgress >= 1
        ? 100
        : Math.floor(displayedProgress * 100);

    loadingProgress.bar?.classList.remove('is-indeterminate');
    loadingProgress.fill?.style.setProperty('transform', `scaleX(${displayedProgress.toFixed(4)})`);
    loadingProgress.bar?.setAttribute('aria-valuenow', String(percent));

    if (status && loadingProgress.status) {
        loadingProgress.status.textContent = status;
    }

    if (loadingProgress.detail) {
        loadingProgress.detail.textContent = detail;
    }

    loadingProgress.bar?.setAttribute(
        'aria-valuetext',
        `${status || 'Loading Calculator'}, ${percent}%${detail ? `, ${detail}` : ''}`);
}

function setLoadingIndeterminate(status, detail = '') {
    loadingProgress.bar?.classList.add('is-indeterminate');
    loadingProgress.bar?.removeAttribute('aria-valuenow');

    if (status && loadingProgress.status) {
        loadingProgress.status.textContent = status;
    }

    if (loadingProgress.detail) {
        loadingProgress.detail.textContent = detail;
    }

    loadingProgress.bar?.setAttribute(
        'aria-valuetext',
        status || 'Starting Calculator');
}

const kilobyteFormatter = new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 });

function formatByteCount(bytes) {
    return `${kilobyteFormatter.format(Math.max(0, Math.floor(bytes / 1024)))} KB`;
}

function formatDownloadDetail(snapshot) {
    if (snapshot.totalBytes <= 0) {
        return 'Measuring application files';
    }

    return `${formatByteCount(snapshot.loadedBytes)} of ${formatByteCount(snapshot.totalBytes)}`;
}

const neutralApplicationCulture = 'en-US';

function canonicalCulture(value) {
    if (typeof value !== 'string' || value.length === 0) {
        return null;
    }

    try {
        return Intl.getCanonicalLocales(value)[0] ?? null;
    } catch {
        return null;
    }
}

function selectApplicationCulture(config) {
    const satelliteResources = config.resources?.satelliteResources;
    if (!satelliteResources || typeof satelliteResources !== 'object') {
        return { culture: neutralApplicationCulture, satelliteCulture: null };
    }

    const availableCultures = Object.keys(satelliteResources);
    const availableByCanonicalName = new Map();
    for (const culture of availableCultures) {
        const canonicalName = canonicalCulture(culture);
        if (canonicalName) {
            availableByCanonicalName.set(canonicalName.toLowerCase(), culture);
        }
    }

    const requestedCultures = [];
    if (config.applicationCulture) {
        requestedCultures.push(config.applicationCulture);
    }
    if (Array.isArray(globalThis.navigator?.languages)) {
        requestedCultures.push(...globalThis.navigator.languages);
    } else if (globalThis.navigator?.language) {
        requestedCultures.push(globalThis.navigator.language);
    }

    for (const requestedCulture of requestedCultures) {
        const canonicalName = canonicalCulture(requestedCulture);
        if (!canonicalName) {
            continue;
        }

        if (canonicalName.toLowerCase() === neutralApplicationCulture.toLowerCase()) {
            return { culture: neutralApplicationCulture, satelliteCulture: null };
        }

        const exactCulture = availableByCanonicalName.get(canonicalName.toLowerCase());
        if (exactCulture) {
            return { culture: exactCulture, satelliteCulture: exactCulture };
        }

        const languagePrefix = `${canonicalName.split('-')[0].toLowerCase()}-`;
        const languageCulture = availableCultures.find(culture =>
            culture.toLowerCase().startsWith(languagePrefix));
        if (languageCulture) {
            return { culture: languageCulture, satelliteCulture: languageCulture };
        }
    }

    return { culture: neutralApplicationCulture, satelliteCulture: null };
}

function configureApplicationCulture(config) {
    const selection = selectApplicationCulture(config);
    config.applicationCulture = selection.culture;

    const satelliteResources = config.resources?.satelliteResources;
    if (!satelliteResources || typeof satelliteResources !== 'object') {
        return;
    }

    if (selection.satelliteCulture) {
        config.resources.satelliteResources = {
            [selection.satelliteCulture]: satelliteResources[selection.satelliteCulture],
        };
        config.loadAllSatelliteResources = true;
        return;
    }

    config.resources.satelliteResources = {};
    config.loadAllSatelliteResources = false;
}

async function importAssetSizes() {
    try {
        const module = await import(`./browser-asset-sizes.js?v=${encodeURIComponent(cacheBustVersion)}`);
        return module.assetSizes instanceof Map ? module.assetSizes : new Map();
    } catch (error) {
        browserTelemetry?.mark('download-size-manifest-unavailable', describeError(error));
        return new Map();
    }
}

function waitForPaint() {
    return new Promise((resolve) => requestAnimationFrame(() => resolve()));
}

function dismissSplashWhenAvaloniaStarts() {
    const host = document.getElementById('out');
    if (!host || !loadingProgress.root) {
        return;
    }

    const startupDeadline = performance.now() + 30000;
    const dismissIfReady = () => {
        const canvas = host.querySelector(':scope > canvas.avalonia-canvas');
        if (!canvas) {
            return false;
        }

        const presentedFrames = Number(canvas.__avaloniaFramesPresented);
        const hasPresentationInstrumentation = canvas.__avaloniaFramesPresented !== undefined;
        const hasPresentedFrame = hasPresentationInstrumentation
            ? Number.isFinite(presentedFrames) && presentedFrames > 0
            : canvas.width > 0 && canvas.height > 0 &&
                (canvas.width !== 300 || canvas.height !== 150);
        if (!hasPresentedFrame) {
            return false;
        }

        loadingProgress.root?.remove();
        browserTelemetry?.mark('avalonia-first-frame-presented');
        scheduleInputReplay();
        return true;
    };

    const waitForFirstFrame = now => {
        if (fatalErrorVisible || !loadingProgress.root?.isConnected || dismissIfReady()) {
            return;
        }

        if (now >= startupDeadline) {
            showFatalError(
                new Error('Avalonia did not present its first frame within 30 seconds.'),
                'startup');
            return;
        }

        requestAnimationFrame(waitForFirstFrame);
    };

    requestAnimationFrame(waitForFirstFrame);
}

function scheduleInputReplay() {
    if (inputReplayScheduled || !inputReplaySessionId) {
        return;
    }

    inputReplayScheduled = true;
    setTimeout(async () => {
        try {
            browserTelemetry?.mark('input-replay-start', `session=${inputReplaySessionId}`);
            const replayModule = await import(`./browser-input-replay.js?v=${encodeURIComponent(cacheBustVersion)}`);
            const result = await replayModule.replayBrowserInputSession(pageUrl, inputReplaySessionId);
            browserTelemetry?.mark('input-replay-complete', `events=${result.eventCount}; durationMs=${Math.round(result.durationMs)}`);
        } catch (error) {
            browserTelemetry?.recordError('input-replay-error', error);
            console.error('Browser input replay failed:', error);
        }
    }, 1000);
}

function scheduleAotProfileCapture(dotnetRuntime) {
    if (!collectAotProfile) {
        return;
    }

    setTimeout(async () => {
        try {
            const profileData = dotnetRuntime.INTERNAL.aotProfileData;
            if (!(profileData instanceof Uint8Array) || profileData.byteLength === 0) {
                throw new Error('The AOT profiler returned no data.');
            }

            const response = await fetch('/aot-profile', {
                method: 'POST',
                headers: { 'Content-Type': 'application/octet-stream' },
                body: profileData,
            });
            if (!response.ok) {
                throw new Error(`AOT profile upload failed: ${response.status}`);
            }

            console.info(`AOT profile captured (${profileData.byteLength} bytes).`);
        } catch (error) {
            console.error('AOT profile capture failed:', error);
        }
    }, (aotProfileDelaySeconds * 1000) + 250);
}

async function boot() {
    browserTelemetry?.setBootStage('runtime-import-start');
    browserTelemetry?.mark('runtime-import-start');
    setLoadingIndeterminate('Loading runtime');
    await waitForPaint();

    const [{ dotnet }, progressModule, assetSizes] = await Promise.all([
        import(`./_framework/dotnet.js?v=${encodeURIComponent(cacheBustVersion)}`),
        import(`./browser-download-progress.js?v=${encodeURIComponent(cacheBustVersion)}`),
        importAssetSizes(),
    ]);
    browserTelemetry?.mark('runtime-import-complete');
    browserTelemetry?.setBootStage('runtime-create-start');
    setLoadingProgress(0, 'Loading application', 'Preparing application files');
    await waitForPaint();

    let startupPhase = false;
    const downloadTracker = progressModule.createDownloadProgressTracker({
        assetSizes,
        onProgress(snapshot) {
            if (startupPhase) {
                return;
            }

            setLoadingProgress(
                snapshot.ratio,
                'Loading application',
                formatDownloadDetail(snapshot));
        },
    });
    let dotnetBuilder = dotnet
        .withModuleConfig({
            // Match WasmInitialHeapSize explicitly so every runtime entry path
            // allocates shared memory before any pthread instance is created.
            INITIAL_MEMORY: initialWasmMemoryBytes,
            onConfigLoaded(config) {
                configureApplicationCulture(config);
                const snapshot = downloadTracker.registerConfiguration(config);
                browserTelemetry?.mark(
                    'download-plan-ready',
                    `assets=${snapshot.totalAssets}; bytes=${snapshot.totalBytes}`);
            },
            onAbort(reason) {
                const error = reason instanceof Error
                    ? reason
                    : new Error(`WebAssembly runtime aborted: ${String(reason)}`);
                showFatalError(error, 'wasm-abort');
            },
        })
        .withResourceLoader(downloadTracker.loadBootResource)
        .withConfig({
            pthreadPoolInitialSize,
            pthreadPoolUnusedSize,
        })
        .withDiagnosticTracing(pageUrl.searchParams.get('runtime-diagnostics') === '1')
        .withApplicationArgumentsFromQuery();

    if (collectAotProfile) {
        dotnetBuilder = dotnetBuilder
            .withEnvironmentVariable('CALCULATOR_AOT_PROFILE_DELAY_SECONDS', String(aotProfileDelaySeconds))
            .withConfig({
                aotProfilerOptions: {
                    writeAt: 'CalculatorApp.Browser.AotProfileCapture::WriteProfile',
                    sendTo: 'System.Runtime.InteropServices.JavaScript.JavaScriptExports::DumpAotProfileData',
                },
            });
    }

    const dotnetRuntime = await dotnetBuilder.create();
    browserTelemetry?.mark('runtime-create-complete');
    const loadedFiles = dotnetRuntime.INTERNAL?.mono_wasm_get_loaded_files?.() ?? [];
    const loadedSatelliteFiles = loadedFiles.filter(file =>
        typeof file === 'string' && file.includes('/Calculator.resources.'));
    browserTelemetry?.mark(
        'satellite-assets-resident',
        loadedSatelliteFiles.length > 0 ? loadedSatelliteFiles.join(',') : 'none');
    if (loadedSatelliteFiles.length > 0) {
        const loadSatellite = dotnetRuntime.Module?._calc_browser_load_configured_satellite;
        if (typeof loadSatellite !== 'function') {
            throw new Error('The native satellite assembly loader is unavailable.');
        }

        const satelliteLoadResult = loadSatellite();
        browserTelemetry?.mark('satellite-native-hook-install', `result=${satelliteLoadResult}`);
        if (satelliteLoadResult !== 1) {
            throw new Error(`Native satellite assembly hook installation failed (${satelliteLoadResult}).`);
        }

        const getSatelliteStatus = dotnetRuntime.Module?._calc_browser_get_satellite_load_status;
        if (typeof getSatelliteStatus === 'function') {
            let lastSatelliteStatus = Number.NaN;
            const satelliteStatusTimer = globalThis.setInterval(() => {
                const status = getSatelliteStatus();
                if (status === lastSatelliteStatus) {
                    return;
                }

                lastSatelliteStatus = status;
                browserTelemetry?.mark('satellite-native-hook-status', `status=${status}`);
                if (status === 2 || status < 0) {
                    globalThis.clearInterval(satelliteStatusTimer);
                }
            }, 25);
        }
    }
    const pthreads = dotnetRuntime.Module?.PThread;
    browserTelemetry?.mark(
        'pthread-pool-ready',
        `running=${pthreads?.runningWorkers?.length ?? -1}; ` +
        `unused=${pthreads?.unusedWorkers?.length ?? -1}; ` +
        `initial=${pthreadPoolInitialSize}; reserve=${pthreadPoolUnusedSize}`);

    startupPhase = true;
    setLoadingIndeterminate('Starting Calculator');
    await waitForPaint();

    const config = dotnetRuntime.getConfig();
    browserTelemetry?.attachRuntime(dotnetRuntime);
    startRuntimeHealthMonitor(dotnetRuntime);
    dismissSplashWhenAvaloniaStarts();
    scheduleAotProfileCapture(dotnetRuntime);
    browserTelemetry?.setBootStage('run-main-start');
    browserTelemetry?.mark('run-main-start');
    await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
    browserTelemetry?.setBootStage('running');
    browserTelemetry?.mark('run-main-complete');
}

try {
    await boot();
} catch (error) {
    setLoadingProgress(1, loadingFailureStatus(error));
    browserTelemetry?.setBootStage('load-failed');
    browserTelemetry?.recordError('boot-error', error);
    console.error('Calculator browser load failed:', error);
    showFatalError(error, 'boot');
}
