#include <stddef.h>
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef struct _MonoAssembly MonoAssembly;
typedef struct _MonoAssemblyLoadContext MonoAssemblyLoadContext;
typedef struct _MonoAssemblyName MonoAssemblyName;
typedef struct _MonoClass MonoClass;
typedef struct _MonoError MonoError;
typedef struct _MonoImage MonoImage;
typedef struct _MonoObject MonoObject;
typedef struct _MonoString MonoString;
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
extern const char *mono_assembly_name_get_culture(MonoAssemblyName *assembly_name);
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

// LLVM-only Mono uses a native int* exception solely as an unwind token. The
// corresponding managed exception remains rooted in the current thread's JIT
// TLS until the generated catch path consumes it. A pthread that unwinds into
// JavaScript has no generated managed catch path, so expose the TLS payload to
// the worker's terminal error handler before that worker exits.
extern MonoObject *mini_llvmonly_load_exception(void);
extern void mini_llvmonly_clear_exception(void);
extern MonoClass *mono_object_get_class(MonoObject *object);
extern const char *mono_class_get_name(MonoClass *klass);
extern const char *mono_class_get_namespace(MonoClass *klass);
extern MonoString *mono_object_to_string(MonoObject *object, MonoObject **exception);
extern char *mono_string_to_utf8(MonoString *string_object);
extern void mono_free(void *memory);

static char *calc_copy_exception_text(
    const char *namespace_name,
    const char *class_name,
    const char *formatted)
{
    const char *safe_namespace = namespace_name == NULL ? "" : namespace_name;
    const char *safe_class = class_name == NULL ? "managed exception" : class_name;
    const char *separator = safe_namespace[0] == '\0' ? "" : ".";
    const char *detail_separator = formatted == NULL ? "" : "\n";
    const char *safe_formatted = formatted == NULL ? "" : formatted;
    size_t namespace_length = strlen(safe_namespace);
    size_t separator_length = strlen(separator);
    size_t class_length = strlen(safe_class);
    size_t detail_separator_length = strlen(detail_separator);
    size_t formatted_length = strlen(safe_formatted);
    size_t result_length = namespace_length
        + separator_length
        + class_length
        + detail_separator_length
        + formatted_length;
    char *result = malloc(result_length + 1);

    if (result == NULL)
    {
        return NULL;
    }

    char *cursor = result;
    memcpy(cursor, safe_namespace, namespace_length);
    cursor += namespace_length;
    memcpy(cursor, separator, separator_length);
    cursor += separator_length;
    memcpy(cursor, safe_class, class_length);
    cursor += class_length;
    memcpy(cursor, detail_separator, detail_separator_length);
    cursor += detail_separator_length;
    memcpy(cursor, safe_formatted, formatted_length);
    cursor[formatted_length] = '\0';
    return result;
}

char *calc_browser_take_current_managed_exception(void)
{
    MonoObject *exception = mini_llvmonly_load_exception();

    if (exception == NULL)
    {
        return NULL;
    }

    MonoClass *exception_class = mono_object_get_class(exception);
    const char *namespace_name = exception_class == NULL
        ? ""
        : mono_class_get_namespace(exception_class);
    const char *class_name = exception_class == NULL
        ? "managed exception"
        : mono_class_get_name(exception_class);
    MonoObject *format_exception = NULL;
    MonoString *formatted_string = mono_object_to_string(exception, &format_exception);
    char *formatted = format_exception == NULL && formatted_string != NULL
        ? mono_string_to_utf8(formatted_string)
        : NULL;
    char *result = calc_copy_exception_text(namespace_name, class_name, formatted);

    if (formatted != NULL)
    {
        mono_free(formatted);
    }

    mini_llvmonly_clear_exception();
    return result;
}

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
// so the parent reopen fails even when Calculator.resources is registered.
// Wait until Calculator is loaded, preload its configured satellite in the
// parent's ALC, then satisfy Mono's identity search from that stable pointer.
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
