#if COLLECT_AOT_PROFILE
using System.Globalization;
using System.Reflection;
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

        Type exportsType = Type.GetType(
            "System.Runtime.InteropServices.JavaScript.JavaScriptExports, System.Runtime.InteropServices.JavaScript",
            throwOnError: true)!;
        MethodInfo stopProfile = exportsType.GetMethod(
            "StopProfile",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(exportsType.FullName, "StopProfile");
        stopProfile.Invoke(null, null);
    }
}
#endif
