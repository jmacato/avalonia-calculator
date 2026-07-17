using System.Runtime.InteropServices.JavaScript;
using Avalonia.Threading;
using Graphing;

namespace CalculatorApp.Browser;

internal static class BrowserGraphPipelineTelemetry
{
    private const string GlobalPropertyName = "calculatorGraphPipeline";
    private static DispatcherTimer? s_timer;

    public static void Start()
    {
        try
        {
            using JSObject? location = JSHost.GlobalThis.GetPropertyAsJSObject("location");
            string search = location?.GetPropertyAsString("search") ?? string.Empty;
            if (!search.Contains("telemetry=1", StringComparison.Ordinal))
            {
                return;
            }

            var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher.UIThread)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += Publish;
            s_timer = timer;
            Publish(null, EventArgs.Empty);
            timer.Start();
        }
        catch (JSException)
        {
            s_timer = null;
        }
    }

    private static void Publish(object? sender, EventArgs eventArgs)
    {
        try
        {
            JSHost.GlobalThis.SetProperty(GlobalPropertyName, string.Join(',', GraphPipelineDiagnostics.Capture()));
        }
        catch (JSException)
        {
            DispatcherTimer? timer = s_timer;
            s_timer = null;
            if (timer is not null)
            {
                timer.Stop();
                timer.Tick -= Publish;
            }
        }
    }
}
