#include <stdbool.h>
#include <stddef.h>

void *sk_graphite_webgpu_render_target_create(void *context, int width, int height)
{
    (void)context;
    (void)width;
    (void)height;
    return NULL;
}

void *sk_graphite_webgpu_surface_create(void *context, int texture_handle)
{
    (void)context;
    (void)texture_handle;
    return NULL;
}

bool sk_graphite_webgpu_context_submit(void *context)
{
    (void)context;
    return false;
}

void sk_graphite_webgpu_context_release_texture(void *context)
{
    (void)context;
}
