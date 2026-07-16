using Avalonia;
using Avalonia.Browser;
using CalculatorApp;

namespace CalculatorApp.Browser;

internal sealed partial class Program
{
    private static async Task Main(string[] args)
    {
        try
        {
            App.SettingsStore = BrowserSettingsStore.Create();
            await BuildAvaloniaApp().StartBrowserAppAsync("out");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>();
}
