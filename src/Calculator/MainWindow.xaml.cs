using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace CalculatorApp;

/// <summary>
/// Hosts the original MainPage content and provides the platform title-bar fallback.
/// </summary>
public sealed partial class MainWindow : Window
{
    public static MainWindow? CurrentInstance { get; private set; }

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        CurrentInstance = this;
        BackButton.Click += OnBackClicked;

        var pageFrame = this.FindControl<ContentControl>("PageFrame")
                        ?? throw new InvalidOperationException("Page host was not created.");
        pageFrame.Content = new MainPage();
    }

    public Button BackButton => this.FindControl<Button>("AppTitleBarBackButton")
                                ?? throw new InvalidOperationException("Back button was not created.");

    private static void OnBackClicked(object? sender, RoutedEventArgs e)
    {
        // The original Frame back stack is replaced by explicit view state while
        // the existing MainPage navigation code is translated.
    }
}
