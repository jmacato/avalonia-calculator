using Avalonia;
using CalculatorApp.Automation;
using Serilog;
using Serilog.Events;

namespace CalculatorApp.Desktop;

internal static class Program
{
    private static AutomationServer? s_automationServer;

    [STAThread]
    public static int Main(string[] args)
    {
        InitializeDiagnostics();
        App.DesktopWindowCreated = window =>
            s_automationServer = AutomationServer.StartFromEnvironment(window);

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            if (s_automationServer is not null)
            {
                s_automationServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        AppBuilder builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CALCULATOR_AUTOMATION_PORT")))
        {
            // Keep flyouts in the visual tree so the loopback automation server
            // can target Avalonia events and directly render the complete surface.
            builder = builder
                .With(new AvaloniaNativePlatformOptions { OverlayPopups = true })
                .With(new Win32PlatformOptions { OverlayPopups = true })
                .With(new X11PlatformOptions { OverlayPopups = true });
        }

        return builder;
    }

    private static void InitializeDiagnostics()
    {
        string logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "io.github.jmacato.calculator",
            "logs");
        Directory.CreateDirectory(logDirectory);

        bool verbose = string.Equals(
            Environment.GetEnvironmentVariable("CALCULATOR_DIAGNOSTICS"),
            "verbose",
            StringComparison.OrdinalIgnoreCase);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(verbose ? LogEventLevel.Information : LogEventLevel.Warning)
            .WriteTo.File(
                Path.Combine(logDirectory, "calculator-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 1_048_576,
                rollOnFileSizeLimit: true,
                shared: true)
            .CreateLogger();
    }
}
