using System.Globalization;
using Avalonia;
using CalculatorApp.Automation;
using CalculatorApp.Desktop.Services;
using CalculatorApp.Services.Settings;
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
        App.SettingsStore = JsonSettingsStore.CreateDefault();
        using var miniModeService =
            new DesktopMiniModeService(App.SettingsStore);
        App.MiniModeService = miniModeService;
        App.DesktopWindowCreated = window =>
        {
            miniModeService.Attach(window);
            s_automationServer = AutomationServer.StartFromEnvironment(window);
        };

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            CalculatorLog.Configure(null);
            if (s_automationServer is not null)
            {
                s_automationServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
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
                formatProvider: CultureInfo.InvariantCulture,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 1_048_576,
                rollOnFileSizeLimit: true,
                shared: true)
            .CreateLogger();
        CalculatorLog.Configure(WriteLog);
    }

    private static void WriteLog(
        CalculatorLogLevel level,
        Exception? exception,
        string messageTemplate,
        object?[] values)
    {
        LogEventLevel serilogLevel = level switch
        {
            CalculatorLogLevel.Information => LogEventLevel.Information,
            CalculatorLogLevel.Warning => LogEventLevel.Warning,
            CalculatorLogLevel.Error => LogEventLevel.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };
        Log.Write(serilogLevel, exception, messageTemplate, values);
    }
}
