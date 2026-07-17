using Avalonia;
using Avalonia.Browser;
using CalculatorApp;

namespace CalculatorApp.Browser;

internal sealed partial class Program
{
    private static async Task Main(string[] args)
    {
#if COLLECT_AOT_PROFILE
        AotProfileCapture.Start();
#endif
        try
        {
            App.SettingsStore = BrowserSettingsStore.Create();
            await BuildAvaloniaApp().StartBrowserAppAsync(
                "out",
                new BrowserPlatformOptions
                {
                    // Browser input is handed to the UI worker through Avalonia's
                    // native atomic event ring and futex wakeup. Keep the managed
                    // dispatcher enabled so hot input never uses per-event Promises.
                    PreferManagedThreadDispatcher = true,
                }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync(exception.ToString()).ConfigureAwait(false);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .AfterSetup(_ => BrowserGraphPipelineTelemetry.Start());
}
