using Avalonia;
using Avalonia.iOS;
using Foundation;

namespace CalculatorApp.iOS;

/// <summary>
/// Connects the iOS process lifetime to the shared Calculator application.
/// </summary>
[Register(nameof(AppHost))]
internal sealed class AppHost : AvaloniaAppDelegate<App>
{
    /// <summary>
    /// Applies Calculator-specific configuration to Avalonia's iOS builder.
    /// </summary>
    /// <param name="builder">The iOS application builder.</param>
    /// <returns>The configured application builder.</returns>
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder);
    }
}
