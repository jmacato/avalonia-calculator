#if COLLECT_AOT_PROFILE
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;

namespace CalculatorApp.Browser;

internal static partial class AotProfileExports
{
    [JSExport]
    [MethodImpl(MethodImplOptions.NoInlining)]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075",
        Justification = "The profiler build keeps JavaScriptExports.StopProfile by its native write-at-method configuration.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "The profiler build keeps JavaScriptExports.StopProfile by its native write-at-method configuration.")]
    public static void Stop()
    {
        var exportsType = typeof(JSHost).Assembly.GetType(
            "System.Runtime.InteropServices.JavaScript.JavaScriptExports",
            throwOnError: true)!;
        var stopProfile = exportsType.GetMethod(
            "StopProfile",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(exportsType.FullName, "StopProfile");
        stopProfile.Invoke(null, null);
    }
}
#endif
