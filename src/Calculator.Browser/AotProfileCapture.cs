#if COLLECT_AOT_PROFILE
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Threading;

namespace CalculatorApp.Browser;

internal static class AotProfileCapture
{
    private const string DelayEnvironmentVariable = "CALCULATOR_AOT_PROFILE_DELAY_SECONDS";
    private static DispatcherTimer? s_timer;

    public static void Start()
    {
        string? value = Environment.GetEnvironmentVariable(DelayEnvironmentVariable);
        if (!double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double seconds) ||
            !double.IsFinite(seconds) ||
            seconds <= 0)
        {
            return;
        }

        var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher.UIThread)
        {
            Interval = TimeSpan.FromSeconds(seconds)
        };
        timer.Tick += Stop;
        s_timer = timer;
        timer.Start();
    }

    private static void Stop(object? sender, EventArgs eventArgs)
    {
        DispatcherTimer? timer = s_timer;
        s_timer = null;
        if (timer is not null)
        {
            timer.Stop();
            timer.Tick -= Stop;
        }

        WriteProfile();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteProfile()
    {
        // The Mono AOT profiler writes immediately before entering this marker.
    }
}
#endif
