#include <stddef.h>
#include <stdatomic.h>
#include <string.h>

typedef struct _MonoAssembly MonoAssembly;
typedef struct _MonoAssemblyLoadContext MonoAssemblyLoadContext;
typedef struct _MonoAssemblyName MonoAssemblyName;
typedef struct _MonoError MonoError;
typedef struct _MonoImage MonoImage;
typedef int MonoImageOpenStatus;
typedef int (*MonoAssemblyCandidatePredicate)(MonoAssembly *assembly, void *user_data);
typedef struct _MonoAssemblyLoadRequest
{
    MonoAssemblyLoadContext *alc;
    MonoAssemblyCandidatePredicate predicate;
    void *predicate_user_data;
    int no_invoke_search_hook;
    int no_managed_load_event;
} MonoAssemblyLoadRequest;
typedef void (*MonoAssemblyLoadFuncV2)(
    MonoAssemblyLoadContext *alc,
    MonoAssembly *assembly,
    void *user_data,
    MonoError *error);
typedef MonoAssembly *(*MonoAssemblySearchFuncV2)(
    MonoAssemblyLoadContext *alc,
    MonoAssembly *requesting,
    MonoAssemblyName *assembly_name,
    int post_load,
    void *user_data,
    MonoError *error);

enum
{
    MONO_IMAGE_OK = 0,
};

extern char *monoeg_g_getenv(const char *variable);
extern void mono_install_assembly_load_hook_v2(
    MonoAssemblyLoadFuncV2 callback,
    void *user_data,
    int append);
extern void mono_install_assembly_search_hook_v2(
    MonoAssemblySearchFuncV2 callback,
    void *user_data,
    int post_load,
    int append);
extern MonoAssemblyName *mono_assembly_get_name(MonoAssembly *assembly);
extern const char *mono_assembly_name_get_name(MonoAssemblyName *assembly_name);
extern MonoImage *mono_assembly_open_from_bundle(
    MonoAssemblyLoadContext *alc,
    const char *file_name,
    MonoImageOpenStatus *status,
    const char *culture);
extern void mono_assembly_request_prepare_load(
    MonoAssemblyLoadRequest *request,
    MonoAssemblyLoadContext *alc);
extern MonoAssembly *mono_assembly_request_load_from(
    MonoImage *image,
    const char *file_name,
    const MonoAssemblyLoadRequest *request,
    MonoImageOpenStatus *status);
extern void mono_image_close(MonoImage *image);

static _Atomic int calc_satellite_loader_installed;
static _Atomic int calc_satellite_load_status;
static _Atomic(MonoAssembly *) calc_satellite_assembly;

static size_t calc_get_configured_culture(char *culture, size_t capacity)
{
    const char *language = monoeg_g_getenv("LANG");
    const char *suffix;
    size_t culture_length;

    if (language == NULL || language[0] == '\0' || capacity == 0)
    {
        return 0;
    }

    suffix = strstr(language, ".UTF-8");
    culture_length = suffix == NULL
        ? strlen(language)
        : (size_t)(suffix - language);
    if (culture_length == 0 || culture_length >= capacity)
    {
        return 0;
    }

    memcpy(culture, language, culture_length);
    culture[culture_length] = '\0';
    return culture_length;
}

static MonoAssembly *calc_find_loaded_satellite(
    MonoAssemblyLoadContext *alc,
    MonoAssembly *requesting,
    MonoAssemblyName *assembly_name,
    int post_load,
    void *user_data,
    MonoError *error)
{
    const char *requested_name;
    MonoAssembly *satellite;

    (void)alc;
    (void)requesting;
    (void)post_load;
    (void)user_data;
    (void)error;

    if (assembly_name == NULL)
    {
        return NULL;
    }

    requested_name = mono_assembly_name_get_name(assembly_name);
    if (requested_name == NULL
        || strcmp(requested_name, "Calculator.resources") != 0)
    {
        return NULL;
    }

    satellite = atomic_load(&calc_satellite_assembly);
    if (satellite != NULL)
    {
        atomic_store(&calc_satellite_load_status, 2);
    }

    return satellite;
}

static void calc_load_configured_satellite(
    MonoAssemblyLoadContext *alc,
    MonoAssembly *assembly,
    void *user_data,
    MonoError *error)
{
    const char satellite_file_name[] = "Calculator.resources.dll";
    MonoAssemblyName *assembly_name;
    const char *simple_name;
    char culture[96];
    MonoImageOpenStatus status = MONO_IMAGE_OK;
    MonoImage *image;
    MonoAssemblyLoadRequest request;
    MonoAssembly *satellite;

    (void)user_data;
    (void)error;

    assembly_name = mono_assembly_get_name(assembly);
    simple_name = assembly_name == NULL
        ? NULL
        : mono_assembly_name_get_name(assembly_name);
    if (simple_name == NULL || strcmp(simple_name, "Calculator") != 0)
    {
        int expected = 0;
        atomic_compare_exchange_strong(
            &calc_satellite_load_status,
            &expected,
            10);
        return;
    }

    atomic_store(&calc_satellite_load_status, 20);

    if (calc_get_configured_culture(culture, sizeof(culture)) == 0)
    {
        atomic_store(&calc_satellite_load_status, -1);
        return;
    }

    image = mono_assembly_open_from_bundle(
        alc,
        satellite_file_name,
        &status,
        culture);
    if (image == NULL)
    {
        atomic_store(&calc_satellite_load_status, -100 - (int)status);
        return;
    }

    mono_assembly_request_prepare_load(&request, alc);
    request.no_invoke_search_hook = 1;
    satellite = mono_assembly_request_load_from(
        image,
        satellite_file_name,
        &request,
        &status);
    mono_image_close(image);
    if (satellite == NULL)
    {
        atomic_store(&calc_satellite_load_status, -200 - (int)status);
        return;
    }

    atomic_store(&calc_satellite_assembly, satellite);
    atomic_store(&calc_satellite_load_status, 1);
}

// Mono 10's satellite resolver attempts to reopen a parent assembly as a file
// before searching its bundled satellite. Browser assemblies are memory-backed,
// so preload the configured satellite in Calculator's assembly load context.
int calc_browser_load_configured_satellite(void)
{
    if (atomic_load(&calc_satellite_loader_installed) != 0)
    {
        return 1;
    }

    mono_install_assembly_search_hook_v2(
        calc_find_loaded_satellite,
        NULL,
        0,
        0);
    mono_install_assembly_load_hook_v2(
        calc_load_configured_satellite,
        NULL,
        1);
    atomic_store(&calc_satellite_loader_installed, 1);
    atomic_store(&calc_satellite_load_status, 0);
    return 1;
}

int calc_browser_get_satellite_load_status(void)
{
    return atomic_load(&calc_satellite_load_status);
}
