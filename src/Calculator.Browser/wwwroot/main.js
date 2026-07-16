const isBrowser = typeof window !== 'undefined';
const cacheBustVersion = new URL(import.meta.url).searchParams.get('v') ?? `${Date.now()}`;
const pageUrl = new URL(globalThis.location.href);
const aotProfileDelaySeconds = Number(pageUrl.searchParams.get('collect-aot-profile'));
const collectAotProfile = Number.isFinite(aotProfileDelaySeconds) && aotProfileDelaySeconds > 0;
let browserTelemetry = null;

if (!isBrowser) {
    throw new Error('Expected to run in a browser');
}

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
};
let displayedProgress = 0;

function describeError(error) {
    if (error instanceof Error) {
        return `${error.name}: ${error.message}${error.stack ? `\n${error.stack}` : ''}`;
    }

    return String(error);
}

function loadingFailureStatus(error) {
    const text = describeError(error);
    if (/mono_download_assets|download .* failed|load failed|fetch|network/i.test(text)) {
        return 'Runtime asset download failed';
    }

    return 'Load failed';
}

function setLoadingProgress(value, status) {
    displayedProgress = Math.max(displayedProgress, Math.min(1, Math.max(0, value)));
    const percent = Math.round(displayedProgress * 100);

    loadingProgress.fill?.style.setProperty('width', `${percent}%`);
    loadingProgress.bar?.setAttribute('aria-valuenow', String(percent));

    if (status && loadingProgress.status) {
        loadingProgress.status.textContent = status;
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

    const dismissIfReady = () => {
        if (!host.querySelector(':scope > canvas.avalonia-canvas')) {
            return false;
        }

        loadingProgress.root?.remove();
        browserTelemetry?.mark('avalonia-canvas-ready');
        return true;
    };

    if (dismissIfReady()) {
        return;
    }

    const observer = new MutationObserver(() => {
        if (dismissIfReady()) {
            observer.disconnect();
        }
    });

    observer.observe(host, { childList: true });
}

function scheduleAotProfileCapture(dotnetRuntime, mainAssemblyName) {
    if (!collectAotProfile) {
        return;
    }

    setTimeout(async () => {
        try {
            const assemblyExports = await dotnetRuntime.getAssemblyExports(mainAssemblyName);
            const stopProfile = assemblyExports?.CalculatorApp?.Browser?.AotProfileExports?.Stop;
            if (typeof stopProfile !== 'function') {
                throw new Error('The AOT profile collector is not present in this build.');
            }

            stopProfile();
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
    }, aotProfileDelaySeconds * 1000);
}

async function boot() {
    browserTelemetry?.setBootStage('runtime-import-start');
    browserTelemetry?.mark('runtime-import-start');
    setLoadingProgress(0.06, 'Loading runtime');
    await waitForPaint();

    const { dotnet } = await import(`./_framework/dotnet.js?v=${encodeURIComponent(cacheBustVersion)}`);
    browserTelemetry?.mark('runtime-import-complete');
    browserTelemetry?.setBootStage('runtime-create-start');
    setLoadingProgress(0.12, 'Loading application');
    await waitForPaint();

    let dotnetBuilder = dotnet
        .withModuleConfig({
            onDownloadResourceProgress(loaded, total) {
                const ratio = total > 0 ? Math.min(loaded / total, 1) : 0;
                setLoadingProgress(0.12 + ratio * 0.72, 'Loading application');
            },
        })
        .withDiagnosticTracing(false)
        .withApplicationArgumentsFromQuery();

    if (collectAotProfile) {
        dotnetBuilder = dotnetBuilder.withConfig({
            aotProfilerOptions: {
                sendTo: 'System.Runtime.InteropServices.JavaScript.JavaScriptExports::DumpAotProfileData',
            },
        });
    }

    const dotnetRuntime = await dotnetBuilder.create();
    browserTelemetry?.mark('runtime-create-complete');

    setLoadingProgress(0.92, 'Starting Calculator');
    await waitForPaint();

    const config = dotnetRuntime.getConfig();
    await browserTelemetry?.attachRuntime(dotnetRuntime, config.mainAssemblyName);
    dismissSplashWhenAvaloniaStarts();
    scheduleAotProfileCapture(dotnetRuntime, config.mainAssemblyName);
    browserTelemetry?.setBootStage('run-main-start');
    browserTelemetry?.mark('run-main-start');
    await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
    await browserTelemetry?.startManagedProbe();
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
    throw error;
}
