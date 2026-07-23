const trackedBehaviors = new Set([
    'assembly',
    'resource',
    'pdb',
    'icu',
    'vfs',
    'dotnetwasm',
]);

function positiveInteger(value) {
    const number = Number(value);
    return Number.isFinite(number) && number > 0 ? Math.round(number) : 0;
}

function assetFileName(name) {
    const separator = Math.max(name.lastIndexOf('/'), name.lastIndexOf('\\'));
    return separator >= 0 ? name.slice(separator + 1) : name;
}

function preferredIcuAsset(config) {
    const resources = config.resources;
    if (!resources?.icu?.length || config.globalizationMode === 'invariant') {
        return null;
    }

    if (config.globalizationMode === 'custom') {
        return resources.icu[0];
    }

    const culture = config.applicationCulture ?? globalThis.navigator?.languages?.[0] ??
        Intl.DateTimeFormat().resolvedOptions().locale;
    let virtualPath = 'icudt.dat';
    if (culture && config.globalizationMode === 'sharded') {
        const language = culture.split('-')[0];
        virtualPath = language === 'en' ||
            ['fr', 'fr-FR', 'it', 'it-IT', 'de', 'de-DE', 'es', 'es-ES'].includes(culture)
            ? 'icudt_EFIGS.dat'
            : ['zh', 'ko', 'ja'].includes(language)
                ? 'icudt_CJK.dat'
                : 'icudt_no_CJK.dat';
    }

    return resources.icu.find(asset => asset.virtualPath === virtualPath) ?? null;
}

function configuredDownloadAssets(config) {
    if (Array.isArray(config.assets)) {
        return config.assets.filter(asset => trackedBehaviors.has(asset.behavior));
    }

    const resources = config.resources;
    if (!resources) {
        return [];
    }

    const assets = [];
    const append = entries => {
        if (Array.isArray(entries)) {
            assets.push(...entries);
        }
    };

    append(resources.wasmNative);
    append(resources.coreAssembly);
    append(resources.assembly);
    append(resources.coreVfs);
    append(resources.vfs);

    if (config.debugLevel !== 0) {
        append(resources.corePdb);
        append(resources.pdb);
    }

    if (resources.satelliteResources) {
        const selectedSatelliteResources = config.applicationCulture
            ? resources.satelliteResources[config.applicationCulture]
            : null;
        if (selectedSatelliteResources) {
            append(selectedSatelliteResources);
        } else if (config.loadAllSatelliteResources) {
            for (const entries of Object.values(resources.satelliteResources)) {
                append(entries);
            }
        }
    }

    const icu = preferredIcuAsset(config);
    if (icu) {
        assets.push(icu);
    }

    if (Array.isArray(config.appsettings)) {
        const environmentName = `appsettings.${config.applicationEnvironment ?? 'Production'}.json`;
        for (const name of config.appsettings) {
            const fileName = assetFileName(name);
            if (fileName === 'appsettings.json' || fileName === environmentName) {
                assets.push({ name });
            }
        }
    }

    return assets;
}

export function createDownloadProgressTracker(options) {
    const assetSizes = options.assetSizes instanceof Map
        ? options.assetSizes
        : new Map(Object.entries(options.assetSizes ?? {}));
    const requestFrame = options.requestFrame ?? globalThis.requestAnimationFrame.bind(globalThis);
    const onProgress = options.onProgress;
    const registeredAssets = new Set();
    let totalBytes = 0;
    let runtimeCompletedAssets = 0;
    let runtimeTotalAssets = 0;
    let framePending = false;

    function snapshot() {
        const currentTotalAssets = runtimeTotalAssets > 0
            ? runtimeTotalAssets
            : registeredAssets.size;
        return {
            loadedBytes: 0,
            totalBytes,
            completedAssets: runtimeCompletedAssets,
            totalAssets: currentTotalAssets,
            usesByteProgress: false,
            ratio: currentTotalAssets > 0
                ? runtimeCompletedAssets / currentTotalAssets
                : 0,
        };
    }

    function publishProgress() {
        if (framePending) {
            return;
        }

        framePending = true;
        requestFrame(() => {
            framePending = false;
            onProgress?.(snapshot());
        });
    }

    function knownSize(name) {
        return positiveInteger(assetSizes.get(name) ?? assetSizes.get(assetFileName(name)));
    }

    function registerAsset(name) {
        if (!name || registeredAssets.has(name)) {
            return;
        }

        registeredAssets.add(name);
        totalBytes += knownSize(name);
    }

    function registerConfiguration(config) {
        for (const asset of configuredDownloadAssets(config)) {
            registerAsset(asset.name);
        }

        publishProgress();
        return snapshot();
    }

    function reportResourceProgress(loadedResources, totalResources) {
        runtimeTotalAssets = positiveInteger(totalResources);
        runtimeCompletedAssets = Math.min(
            positiveInteger(loadedResources),
            runtimeTotalAssets);
        publishProgress();
        return snapshot();
    }

    return {
        getSnapshot: snapshot,
        registerConfiguration,
        reportResourceProgress,
    };
}
