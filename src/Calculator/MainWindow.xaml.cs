using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace CalculatorApp;

/// <summary>
/// Hosts the original MainPage content and maps WinUI's SetTitleBar behavior to
/// extended client-area chrome where it is reliable.
/// </summary>
public sealed partial class MainWindow : Window
{
    public static MainWindow? CurrentInstance { get; private set; }

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        CurrentInstance = this;
        BackButton.Click += OnBackClicked;

        var windowRoot = this.FindControl<Grid>("WindowRoot")
                         ?? throw new InvalidOperationException("Window root was not created.");
        var appTitleBar = this.FindControl<Grid>("AppTitleBar")
                          ?? throw new InvalidOperationException("App title bar was not created.");
        var pageFrame = this.FindControl<ContentControl>("PageFrame")
                        ?? throw new InvalidOperationException("Page host was not created.");
        ConfigureWindowChrome(windowRoot, appTitleBar, pageFrame);
        pageFrame.Content = new MainPage();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (this.FindControl<ContentControl>("PageFrame") is { } pageFrame &&
            pageFrame.Content is MainPage mainPage)
        {
            pageFrame.Content = null;
            mainPage.Dispose();
        }

        base.OnClosed(e);
    }

    public Button BackButton => this.FindControl<Button>("AppTitleBarBackButton")
                                ?? throw new InvalidOperationException("Back button was not created.");

    private void OnBackClicked(object? sender, RoutedEventArgs e)
    {
        // The original Frame back stack is replaced by explicit view state while
        // the existing MainPage navigation code is translated.
    }

    private void ConfigureWindowChrome(
        Grid windowRoot,
        Grid appTitleBar,
        ContentControl pageFrame)
    {
        if (OperatingSystem.IsWindows())
        {
            // Direct Avalonia equivalent of WinUI Window.SetTitleBar. The
            // attached TitleBar role supplies non-client hit testing while the
            // OS continues to own its caption buttons and resize frame.
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = 48;
            return;
        }

        // macOS and the current Linux backends use their conventional native
        // title bars. Do not stack the WinUI title row beneath native chrome.
        appTitleBar.IsVisible = false;
        windowRoot.RowDefinitions[0].Height = new GridLength(0);
        Grid.SetRow(pageFrame, 0);
        Grid.SetRowSpan(pageFrame, 2);
    }
}
