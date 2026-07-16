using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using Avalonia.Threading;

namespace CalculatorApp.Browser;

internal static partial class BrowserTelemetryExports
{
    private static DispatcherTimer? s_timer;
    private static long s_dispatcherPulse;
    private static long s_lastDispatcherPulseTimestamp;

    [JSExport]
    public static async Task Start()
    {
        if (s_timer is not null)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (s_timer is not null)
            {
                return;
            }

            s_lastDispatcherPulseTimestamp = Stopwatch.GetTimestamp();
            var timer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher.UIThread)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += OnDispatcherPulse;
            s_timer = timer;
            timer.Start();
        });
    }

    [JSExport]
    public static Task<string> GetSnapshot()
    {
        var timestamp = Volatile.Read(ref s_lastDispatcherPulseTimestamp);
        var dispatcherAgeMilliseconds = timestamp == 0
            ? -1
            : Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
        return Task.FromResult(string.Create(
            CultureInfo.InvariantCulture,
            $"{GC.GetTotalMemory(forceFullCollection: false)},{GC.GetTotalAllocatedBytes(precise: false)},{GC.CollectionCount(0)},{GC.CollectionCount(1)},{GC.CollectionCount(2)},{Volatile.Read(ref s_dispatcherPulse)},{dispatcherAgeMilliseconds:R}"));
    }

    private static void OnDispatcherPulse(object? sender, EventArgs eventArgs)
    {
        Interlocked.Increment(ref s_dispatcherPulse);
        Volatile.Write(ref s_lastDispatcherPulseTimestamp, Stopwatch.GetTimestamp());
    }
}
