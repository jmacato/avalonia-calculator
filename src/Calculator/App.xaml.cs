using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CalculatorApp.Services.Settings;

namespace CalculatorApp;

/// <summary>
/// Provides application-specific behavior to supplement the desktop application.
/// </summary>
public sealed partial class App : Application
{
    public static ISettingsStore SettingsStore { get; set; } = new InMemorySettingsStore();

    public static Control? RootView { get; private set; }
#if !CALCULATOR_BROWSER
    public static MainWindow? Window { get; private set; }
    public static Action<MainWindow>? DesktopWindowCreated { get; set; }
#endif

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
#if !CALCULATOR_BROWSER
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Window = new MainWindow();
            RootView = Window;
            desktop.MainWindow = Window;
            DesktopWindowCreated?.Invoke(Window);
        }
        else
#endif
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            RootView = new MainPage();
            singleViewPlatform.MainView = RootView;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
