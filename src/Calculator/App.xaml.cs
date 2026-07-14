using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace CalculatorApp;

/// <summary>
/// Provides application-specific behavior to supplement the desktop application.
/// </summary>
public sealed partial class App : Application
{
    public static MainWindow? Window { get; private set; }
    public static Control? RootView { get; private set; }
    public static Action<MainWindow>? DesktopWindowCreated { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Window = new MainWindow();
            RootView = Window;
            desktop.MainWindow = Window;
            DesktopWindowCreated?.Invoke(Window);
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            RootView = new MainPage();
            singleViewPlatform.MainView = RootView;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
