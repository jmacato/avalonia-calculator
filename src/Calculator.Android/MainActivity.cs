using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace CalculatorApp.Android;

/// <summary>
/// Hosts the shared Calculator view in the primary Android activity.
/// </summary>
[Activity(
    Label = "Calculator",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/calculator",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
}
