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

function responseWithTrackedBody(response, state, updateLoaded, finishAsset) {
    if (!response.body) {
        finishAsset(state);
        return response;
    }

    const reader = response.body.getReader();
    const maximumProgressChunkBytes = 64 * 1024;
    let pendingChunk = null;
    let pendingOffset = 0;
    const body = new ReadableStream({
        async pull(controller) {
            try {
                if (!pendingChunk) {
                    const result = await reader.read();
                    if (result.done) {
                        finishAsset(state);
                        controller.close();
                        return;
                    }

                    pendingChunk = result.value;
                    pendingOffset = 0;
                }

                const endOffset = Math.min(
                    pendingOffset + maximumProgressChunkBytes,
                    pendingChunk.byteLength);
                const deliveredChunk = pendingOffset === 0 && endOffset === pendingChunk.byteLength
                    ? pendingChunk
                    : pendingChunk.subarray(pendingOffset, endOffset);
                pendingOffset = endOffset;
                if (pendingOffset === pendingChunk.byteLength) {
                    pendingChunk = null;
                    pendingOffset = 0;
                }

                const paintOpportunity = updateLoaded(state, deliveredChunk.byteLength);
                controller.enqueue(deliveredChunk);
                if (paintOpportunity) {
                    await paintOpportunity;
                }
            } catch (error) {
                controller.error(error);
            }
        },
        cancel(reason) {
            return reader.cancel(reason);
        },
    });

    return new Response(body, {
        status: response.status,
        statusText: response.statusText,
        headers: response.headers,
    });
}

export function createDownloadProgressTracker(options) {
    const assetSizes = options.assetSizes instanceof Map
        ? options.assetSizes
        : new Map(Object.entries(options.assetSizes ?? {}));
    const fetchResource = options.fetchResource ?? globalThis.fetch.bind(globalThis);
    const requestFrame = options.requestFrame ?? globalThis.requestAnimationFrame.bind(globalThis);
    const scheduleAfterPaint = options.scheduleAfterPaint ??
        (callback => globalThis.setTimeout(callback, 0));
    const onProgress = options.onProgress;
    const states = new Map();
    let loadedBytes = 0;
    let totalBytes = 0;
    let unknownSizeAssets = 0;
    let completedAssets = 0;
    let framePending = false;
    let framePromise = null;
    let resolveFrame = null;
    let loadedBytesAtLastPaint = 0;

    function snapshot() {
        const usesByteProgress = states.size > 0 && unknownSizeAssets === 0;
        return {
            loadedBytes,
            totalBytes,
            completedAssets,
            totalAssets: states.size,
            usesByteProgress,
            ratio: usesByteProgress
                ? Math.min(loadedBytes / totalBytes, 1)
                : states.size > 0 ? completedAssets / states.size : 0,
        };
    }

    function publishProgress() {
        if (framePending) {
            return framePromise;
        }

        framePending = true;
        framePromise = new Promise(resolve => {
            resolveFrame = resolve;
        });
        requestFrame(() => {
            framePending = false;
            onProgress?.(snapshot());
            loadedBytesAtLastPaint = loadedBytes;

            const completeFrame = resolveFrame;
            framePromise = null;
            resolveFrame = null;
            scheduleAfterPaint(completeFrame);
        });

        return framePromise;
    }

    function knownSize(name) {
        return positiveInteger(assetSizes.get(name) ?? assetSizes.get(assetFileName(name)));
    }

    function registerAsset(name) {
        if (!name || states.has(name)) {
            return states.get(name) ?? null;
        }

        const expectedBytes = knownSize(name);
        const state = {
            name,
            expectedBytes,
            loadedBytes: 0,
            complete: false,
        };
        states.set(name, state);
        totalBytes += state.expectedBytes;
        if (state.expectedBytes === 0) {
            unknownSizeAssets++;
        }
        return state;
    }

    function resetAttempt(state) {
        if (state.expectedBytes > 0) {
            loadedBytes -= state.loadedBytes;
        }
        if (state.complete) {
            completedAssets--;
        }

        state.loadedBytes = 0;
        state.complete = false;
    }

    function updateLoaded(state, byteCount) {
        const bytes = positiveInteger(byteCount);
        const previousLoadedBytes = state.loadedBytes;
        state.loadedBytes = state.expectedBytes > 0
            ? Math.min(state.loadedBytes + bytes, state.expectedBytes)
            : state.loadedBytes + bytes;
        if (state.expectedBytes > 0) {
            loadedBytes += state.loadedBytes - previousLoadedBytes;
        }

        const paintOpportunity = publishProgress();
        const targetPaintCount = 120;
        const minimumPaintBatchBytes = 64 * 1024;
        const paintBatchBytes = Math.max(
            minimumPaintBatchBytes,
            Math.ceil(totalBytes / targetPaintCount));
        return loadedBytes - loadedBytesAtLastPaint >= paintBatchBytes
            ? paintOpportunity
            : null;
    }

    function finishAsset(state) {
        if (state.complete) {
            return;
        }

        if (state.expectedBytes > 0) {
            loadedBytes += state.expectedBytes - state.loadedBytes;
            state.loadedBytes = state.expectedBytes;
        }

        state.complete = true;
        completedAssets++;
        publishProgress();
    }

    function registerConfiguration(config) {
        for (const asset of configuredDownloadAssets(config)) {
            registerAsset(asset.name);
        }

        publishProgress();
        return snapshot();
    }

    async function loadTrackedBootResource(name, defaultUri, integrity) {
        const state = registerAsset(name);
        resetAttempt(state);
        publishProgress();

        const request = {
            credentials: 'same-origin',
        };
        if (integrity) {
            request.integrity = integrity;
        }

        const response = await fetchResource(defaultUri, request);
        if (!response.ok) {
            return response;
        }

        return responseWithTrackedBody(response, state, updateLoaded, finishAsset);
    }

    function loadBootResource(_type, name, defaultUri, integrity, behavior) {
        if (!trackedBehaviors.has(behavior)) {
            return undefined;
        }

        return loadTrackedBootResource(name, defaultUri, integrity);
    }

    return {
        getSnapshot: snapshot,
        loadBootResource,
        registerConfiguration,
    };
}
