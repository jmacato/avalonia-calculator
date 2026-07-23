using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace CalculatorApp.Android;

/// <summary>
/// Connects the Android process lifetime to the shared Calculator application.
/// </summary>
[Application]
public class CalculatorApplication : AvaloniaAndroidApplication<App>
{
    /// <summary>
    /// Initializes the Android application wrapper from its Java peer.
    /// </summary>
    /// <param name="javaReference">The native Java object reference.</param>
    /// <param name="transfer">The ownership transfer mode for the reference.</param>
    protected CalculatorApplication(
        nint javaReference,
        JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    /// <summary>
    /// Applies Calculator-specific configuration to Avalonia's Android builder.
    /// </summary>
    /// <param name="builder">The Android application builder.</param>
    /// <returns>The configured application builder.</returns>
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder);
    }
}
