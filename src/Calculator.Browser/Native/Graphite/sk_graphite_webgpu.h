/*
 * Copyright 2026 CalcNeo contributors.
 *
 * Use of this source code is governed by a BSD-style license that can be
 * found in the LICENSE file.
 */

#ifndef sk_graphite_webgpu_DEFINED
#define sk_graphite_webgpu_DEFINED

#include "include/c/sk_types.h"

#include <stddef.h>

SK_C_PLUS_PLUS_BEGIN_GUARD

typedef struct sk_graphite_webgpu_context_t sk_graphite_webgpu_context_t;

SK_C_API sk_graphite_webgpu_context_t* sk_graphite_webgpu_context_create(
        size_t maxResourceBytes);
SK_C_API void sk_graphite_webgpu_context_destroy(sk_graphite_webgpu_context_t* context);
SK_C_API sk_surface_t* sk_graphite_webgpu_render_target_create(
        sk_graphite_webgpu_context_t* context,
        int width,
        int height);
SK_C_API sk_surface_t* sk_graphite_webgpu_surface_create(
        sk_graphite_webgpu_context_t* context,
        int textureHandle);
SK_C_API bool sk_graphite_webgpu_context_submit(sk_graphite_webgpu_context_t* context);
SK_C_API void sk_graphite_webgpu_context_release_texture(
        sk_graphite_webgpu_context_t* context);

SK_C_PLUS_PLUS_END_GUARD

#endif
