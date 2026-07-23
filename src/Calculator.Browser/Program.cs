using Avalonia;
using Avalonia.Browser;

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
            string? satelliteCulture = Environment.GetEnvironmentVariable("CALCULATOR_SATELLITE_CULTURE");
            App.SettingsStore = BrowserSettingsStore.Create();
            await BuildAvaloniaApp().StartBrowserAppAsync(
                "out",
                new BrowserPlatformOptions
                {
                    // Hand browser input to the UI worker through Avalonia's
                    // native atomic event ring. This keeps the hot input path
                    // synchronous and avoids per-event JS-to-managed promises.
                    PreferManagedThreadDispatcher = true,
#if CALC_WEBGPU
                    RenderingMode =
                    [
                        BrowserRenderingMode.WebGPU,
                        BrowserRenderingMode.WebGL2,
                        BrowserRenderingMode.WebGL1,
                        BrowserRenderingMode.Software2D,
                    ],
#endif
                    SatelliteAssemblyCulture = satelliteCulture,
                }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync(exception.ToString()).ConfigureAwait(false);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .AfterSetup(_ => BrowserGraphPipelineTelemetry.Start());
    }
}
