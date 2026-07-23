#pragma once

#include <string>

#include <webgpu/webgpu_cpp.h>

#include "include/private/base/SkLog.h"

// Skia m150's __EMSCRIPTEN__ branches target the former Emscripten WebGPU
// wrapper. Keep that narrow source contract while linking Dawn's current,
// separately versioned Emdawnwebgpu browser implementation.
typedef enum WGPUBufferMapAsyncStatus {
    WGPUBufferMapAsyncStatus_Success = 1,
    WGPUBufferMapAsyncStatus_ValidationError = 3,
    WGPUBufferMapAsyncStatus_Unknown = 4,
    WGPUBufferMapAsyncStatus_DeviceLost = 5,
    WGPUBufferMapAsyncStatus_DestroyedBeforeCallback = 6,
    WGPUBufferMapAsyncStatus_UnmappedBeforeCallback = 7,
    WGPUBufferMapAsyncStatus_MappingAlreadyPending = 8,
    WGPUBufferMapAsyncStatus_OffsetOutOfRange = 9,
    WGPUBufferMapAsyncStatus_SizeOutOfRange = 10,
} WGPUBufferMapAsyncStatus;

namespace wgpu {
using ShaderModuleWGSLDescriptor = ShaderSourceWGSL;
using ImageCopyBuffer = TexelCopyBufferInfo;
using ImageCopyTexture = TexelCopyTextureInfo;
using RenderPassTimestampWrites = PassTimestampWrites;
using ComputePassTimestampWrites = PassTimestampWrites;
using ErrorCallback = void (*)(WGPUErrorType, const char*, void*);

struct SupportedLimits : Limits {
    SupportedLimits() : limits(*this) {}

    Limits& limits;
};
}

// Emdawnwebgpu's current callbacks carry a message and an explicit callback
// mode. Adapt Skia's legacy browser call sites without changing Skia source.
#define MapAsync(mode, offset, size, callback, userdata)                              \
    MapAsync(mode,                                                                    \
             offset,                                                                  \
             size,                                                                    \
             wgpu::CallbackMode::AllowSpontaneous,                                    \
             [calcneoCallback = (callback), calcneoUserdata = (userdata)](            \
                     wgpu::MapAsyncStatus calcneoStatus, wgpu::StringView) {           \
                 calcneoCallback(static_cast<WGPUBufferMapAsyncStatus>(calcneoStatus), \
                                  calcneoUserdata);                                    \
             })

#define PopErrorScope(callback, userdata)                                         \
    PopErrorScope(wgpu::CallbackMode::AllowSpontaneous,                            \
                  [calcneoCallback = (callback), calcneoUserdata = (userdata)](    \
                          wgpu::PopErrorScopeStatus,                                \
                          wgpu::ErrorType calcneoType,                              \
                          wgpu::StringView calcneoMessage) {                        \
                      std::string calcneoText(                                      \
                              calcneoMessage.data ? calcneoMessage.data : "",       \
                              calcneoMessage.data ? calcneoMessage.length : 0);     \
                      calcneoCallback(static_cast<WGPUErrorType>(calcneoType),      \
                                       calcneoText.c_str(),                         \
                                       calcneoUserdata);                            \
                  })

#define OnSubmittedWorkDone(callback, userdata)                                  \
    OnSubmittedWorkDone(wgpu::CallbackMode::AllowSpontaneous,                     \
                        [calcneoCallback = (callback), calcneoUserdata = (userdata)]( \
                                wgpu::QueueWorkDoneStatus calcneoStatus,           \
                                wgpu::StringView) {                                \
                            calcneoCallback(                                       \
                                    static_cast<WGPUQueueWorkDoneStatus>(calcneoStatus), \
                                    calcneoUserdata);                              \
                        })

#define VertexBufferNotUsed Undefined

// Skia's logging macro currently requires a constant priority even though its
// legacy map-error adapter intentionally selects the priority at runtime.
#undef SKIA_LOG
#define SKIA_LOG(priority, fmt, ...)                                      \
    do {                                                                  \
        const SkLogPriority calcneoPriority = (priority);                  \
        if (calcneoPriority <= SKIA_LOWEST_ACTIVE_LOG_PRIORITY) {          \
            SkLog(calcneoPriority, "[skia] " fmt "\n", ##__VA_ARGS__);     \
        }                                                                 \
    } while (0)
