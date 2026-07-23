/*
 * Copyright 2026 CalcNeo contributors.
 *
 * Use of this source code is governed by a BSD-style license that can be
 * found in the LICENSE file.
 */

#include "sk_graphite_webgpu.h"

#if defined(__EMSCRIPTEN__) && defined(SK_GRAPHITE) && defined(SK_DAWN)

#include "include/core/SkColorSpace.h"
#include "include/core/SkImageInfo.h"
#include "include/core/SkSurface.h"
#include "include/core/SkSurfaceProps.h"
#include "include/gpu/graphite/BackendTexture.h"
#include "include/gpu/graphite/Context.h"
#include "include/gpu/graphite/ContextOptions.h"
#include "include/gpu/graphite/Recorder.h"
#include "include/gpu/graphite/Surface.h"
#include "include/gpu/graphite/dawn/DawnBackendContext.h"
#include "include/gpu/graphite/dawn/DawnGraphiteTypes.h"
#include "src/c/sk_types_priv.h"

#include <algorithm>
#include <cstdint>
#include <memory>

struct sk_graphite_webgpu_context_t {
    std::unique_ptr<skgpu::graphite::Context> fContext;
    std::unique_ptr<skgpu::graphite::Recorder> fRecorder;
    WGPUTexture fCurrentTexture = nullptr;
};

sk_graphite_webgpu_context_t* sk_graphite_webgpu_context_create(size_t maxResourceBytes) {
    WGPUDevice rawDevice = emscripten_webgpu_get_device();
    if (!rawDevice) {
        return nullptr;
    }

    skgpu::graphite::DawnBackendContext backend;
    backend.fDevice = wgpu::Device::Acquire(rawDevice);
    backend.fQueue = backend.fDevice.GetQueue();

    skgpu::graphite::ContextOptions options;
    options.fInternalMultisampleCount = skgpu::graphite::SampleCount::k1;
    options.fAllowMultipleAtlasTextures = false;

    // Keep the persistent Context and Recorder within one app-wide budget. Both
    // Graphite objects otherwise default to independent 256 MiB caches.
    constexpr size_t kDefaultResourceBudget = 1024u * 600u * 4u * 12u;
    constexpr size_t kMinimumObjectBudget = 8u * 1024u * 1024u;
    const size_t totalBudget = maxResourceBytes ? maxResourceBytes : kDefaultResourceBudget;
    const size_t contextBudget = std::max(kMinimumObjectBudget, totalBudget / 2u);
    const size_t recorderBudget = std::max(kMinimumObjectBudget, totalBudget - contextBudget);
    options.fGpuBudgetInBytes = contextBudget;
    options.fGlyphCacheTextureMaximumBytes =
            std::min<size_t>(4u * 1024u * 1024u, recorderBudget / 4u);

    auto graphiteContext = skgpu::graphite::ContextFactory::MakeDawn(backend, options);
    if (!graphiteContext) {
        return nullptr;
    }

    skgpu::graphite::RecorderOptions recorderOptions;
    recorderOptions.fGpuBudgetInBytes = recorderBudget;
    auto recorder = graphiteContext->makeRecorder(recorderOptions);
    if (!recorder) {
        return nullptr;
    }

    return new sk_graphite_webgpu_context_t{
            std::move(graphiteContext), std::move(recorder)};
}

void sk_graphite_webgpu_context_destroy(sk_graphite_webgpu_context_t* context) {
    if (!context) {
        return;
    }
    if (context->fCurrentTexture) {
        wgpuTextureRelease(context->fCurrentTexture);
        context->fCurrentTexture = nullptr;
    }
    delete context;
}

sk_surface_t* sk_graphite_webgpu_render_target_create(
        sk_graphite_webgpu_context_t* context,
        int width,
        int height) {
    if (!context || width <= 0 || height <= 0) {
        return nullptr;
    }

    const SkImageInfo imageInfo = SkImageInfo::Make(
            width,
            height,
            kRGBA_8888_SkColorType,
            kPremul_SkAlphaType,
            SkColorSpace::MakeSRGB());
    const SkSurfaceProps surfaceProps(0, kRGB_H_SkPixelGeometry);
    sk_sp<SkSurface> surface = SkSurfaces::RenderTarget(
            context->fRecorder.get(),
            imageInfo,
            skgpu::Mipmapped::kNo,
            &surfaceProps);
    return ToSurface(surface.release());
}

sk_surface_t* sk_graphite_webgpu_surface_create(sk_graphite_webgpu_context_t* context,
                                                int textureHandle) {
    if (!context || textureHandle <= 0 || context->fCurrentTexture) {
        if (textureHandle > 0)
            wgpuTextureRelease(reinterpret_cast<WGPUTexture>(
                    static_cast<uintptr_t>(textureHandle)));
        return nullptr;
    }

    WGPUTexture texture = reinterpret_cast<WGPUTexture>(
            static_cast<uintptr_t>(textureHandle));

    SkColorType colorType;
    switch (wgpuTextureGetFormat(texture)) {
        case WGPUTextureFormat_RGBA8Unorm:
        case WGPUTextureFormat_RGBA8UnormSrgb:
            colorType = kRGBA_8888_SkColorType;
            break;
        case WGPUTextureFormat_BGRA8Unorm:
        case WGPUTextureFormat_BGRA8UnormSrgb:
            colorType = kBGRA_8888_SkColorType;
            break;
        default:
            wgpuTextureRelease(texture);
            return nullptr;
    }

    skgpu::graphite::BackendTexture backendTexture =
            skgpu::graphite::BackendTextures::MakeDawn(texture);
    sk_sp<SkSurface> surface = SkSurfaces::WrapBackendTexture(
            context->fRecorder.get(),
            backendTexture,
            colorType,
            SkColorSpace::MakeSRGB(),
            nullptr);
    if (!surface) {
        wgpuTextureRelease(texture);
        return nullptr;
    }

    // The wrapped surface retains its own texture reference in m150. Keep the
    // imported reference as the current-frame lease and release it only after
    // the managed SKSurface has been disposed.
    context->fCurrentTexture = texture;
    return ToSurface(surface.release());
}

bool sk_graphite_webgpu_context_submit(sk_graphite_webgpu_context_t* context) {
    if (!context) {
        return false;
    }

    std::unique_ptr<skgpu::graphite::Recording> recording = context->fRecorder->snap();
    if (!recording) {
        return false;
    }

    skgpu::graphite::InsertRecordingInfo info;
    info.fRecording = recording.get();
    if (!context->fContext->insertRecording(info)) {
        return false;
    }
    return context->fContext->submit(skgpu::graphite::SyncToCpu::kNo);
}

void sk_graphite_webgpu_context_release_texture(sk_graphite_webgpu_context_t* context) {
    if (!context || !context->fCurrentTexture) {
        return;
    }

    wgpuTextureRelease(context->fCurrentTexture);
    context->fCurrentTexture = nullptr;
}

#else

struct sk_graphite_webgpu_context_t {};
sk_graphite_webgpu_context_t* sk_graphite_webgpu_context_create(size_t) { return nullptr; }
void sk_graphite_webgpu_context_destroy(sk_graphite_webgpu_context_t*) {}
sk_surface_t* sk_graphite_webgpu_render_target_create(
        sk_graphite_webgpu_context_t*, int, int) {
    return nullptr;
}
sk_surface_t* sk_graphite_webgpu_surface_create(sk_graphite_webgpu_context_t*, int) {
    return nullptr;
}
bool sk_graphite_webgpu_context_submit(sk_graphite_webgpu_context_t*) { return false; }
void sk_graphite_webgpu_context_release_texture(sk_graphite_webgpu_context_t*) {}

#endif
