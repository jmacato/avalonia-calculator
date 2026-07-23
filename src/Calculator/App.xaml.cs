using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CalculatorApp.Services.Settings;
using CalculatorApp.Services.Windowing;
using CalculatorApp.ViewModel;
using MathComposer.Avalonia;

namespace CalculatorApp;

/// <summary>
/// Provides application-specific behavior to supplement the desktop application.
/// </summary>
public sealed partial class App : Application
{
    private static int s_dispatcherExceptionHandlerInstalled;

    public static ISettingsStore SettingsStore { get; set; } = new InMemorySettingsStore();

    public static IMiniModeService MiniModeService { get; set; } =
        UnsupportedMiniModeService.Instance;

    public static Control? RootView { get; private set; }
#if !CALCULATOR_BROWSER
    public static MainWindow? Window { get; private set; }
    public static Action<MainWindow>? DesktopWindowCreated { get; set; }
#endif

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        MathFontResources.Initialize();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        InstallDispatcherExceptionHandler();
        SettingsStore.BindToCurrentThread();

#if !CALCULATOR_BROWSER
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Window = new MainWindow();
            RootView = Window;
            desktop.MainWindow = Window;
            DesktopWindowCreated?.Invoke(Window);
        }
#endif
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            var mainPage = new MainPage();
            ConverterPipelineDiagnostics.RecordRootPage(
                mainPage.DiagnosticPageId);
            RootView = mainPage;
            singleViewPlatform.MainView = RootView;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void InstallDispatcherExceptionHandler()
    {
        if (Interlocked.CompareExchange(
                ref s_dispatcherExceptionHandlerInstalled,
                1,
                0) == 0)
        {
            Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
        }
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        _ = sender;

        if (!DispatcherExceptionRecoveryGate.TryRecover(
                eventArgs.Exception,
                out int occurrence))
        {
            if (occurrence != 0)
            {
                Console.Error.WriteLine(
                    $"Calculator dispatcher exception circuit opened after {occurrence} failures: {eventArgs.Exception}");
            }

            return;
        }

        // Mark the fault handled before performing diagnostics. The failed
        // dispatcher operation is already completed with its exception; this
        // only keeps an isolated UI fault from terminating the browser loop.
        eventArgs.Handled = true;
        Console.Error.WriteLine(
            $"Calculator recovered dispatcher exception {occurrence}: {eventArgs.Exception}");
    }
}
