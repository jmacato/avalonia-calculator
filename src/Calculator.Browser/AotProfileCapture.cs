#if COLLECT_AOT_PROFILE
using System.Globalization;
using System.Runtime.CompilerServices;

namespace CalculatorApp.Browser;

internal static class AotProfileCapture
{
    private const string DelayEnvironmentVariable = "CALCULATOR_AOT_PROFILE_DELAY_SECONDS";
    private static Timer? s_timer;

    public static void Start()
    {
        string? value = Environment.GetEnvironmentVariable(DelayEnvironmentVariable);
        if (!double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double seconds) ||
            !double.IsFinite(seconds) ||
            seconds <= 0)
        {
            return;
        }

        var timer = new Timer(
            static _ => Stop(),
            state: null,
            dueTime: TimeSpan.FromSeconds(seconds),
            period: Timeout.InfiniteTimeSpan);
        Interlocked.Exchange(ref s_timer, timer)?.Dispose();
    }

    private static void Stop()
    {
        Interlocked.Exchange(ref s_timer, null)?.Dispose();
        WriteProfile();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteProfile()
    {
        // The Mono AOT profiler writes immediately before entering this marker.
    }
}
#endif
