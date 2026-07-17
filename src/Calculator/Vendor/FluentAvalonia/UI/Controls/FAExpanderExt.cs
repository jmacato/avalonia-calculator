using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Styling;
using FluentAvalonia.Core;

namespace FluentAvalonia.UI.Controls;
/// <summary>
/// Special helper class to enable WinUI like animations on the Expander control
/// </summary>
public sealed class FAExpanderExt : AvaloniaObject
{
    static FAExpanderExt()
    {
        ExpanderAnimationTypeProperty.Changed.Subscribe(new SimpleObserver<AvaloniaPropertyChangedEventArgs>(HandleExpanderAnimationTypeChanged));
    }

    /// <summary>
    /// Defines the ExpanderAnimationType attached property
    /// </summary>
    public static readonly AttachedProperty<string> ExpanderAnimationTypeProperty = AvaloniaProperty.RegisterAttached<FAExpanderExt, Expander, string>("ExpanderAnimationType");
    private static readonly AttachedProperty<FAExpanderExtExpanderInfo> ExpanderAnimationInfoProperty = AvaloniaProperty.RegisterAttached<FAExpanderExt, Expander, FAExpanderExtExpanderInfo>("ExpanderAnimationInfo");
    /// <summary>
    /// Gets the current value of the <see cref = "ExpanderAnimationTypeProperty"/>
    /// </summary>
    public static string GetExpanderAnimationType(Expander exp)
    {
        ArgumentNullException.ThrowIfNull(exp);
        return exp.GetValue(ExpanderAnimationTypeProperty);
    }
    /// <summary>
    /// Sets the current value of the <see cref = "ExpanderAnimationTypeProperty"/>
    /// </summary>
    public static void SetExpanderAnimationType(Expander exp, string value)
    {
        ArgumentNullException.ThrowIfNull(exp);
        exp.SetValue(ExpanderAnimationTypeProperty, value);
    }

    private static void HandleExpanderAnimationTypeChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Sender is not Expander expander)
        {
            return;
        }

        FAExpanderExtExpanderInfo? existingInfo = expander.GetValue(ExpanderAnimationInfoProperty);
        string? value = args.GetNewValue<string>();
        if (string.Equals(value, FluentV2, StringComparison.OrdinalIgnoreCase))
        {
            if (existingInfo is null)
            {
                expander.SetValue(ExpanderAnimationInfoProperty, new FAExpanderExtExpanderInfo(expander));
            }
        }
        else
        {
            existingInfo?.Detach();
            expander.ClearValue(ExpanderAnimationInfoProperty);
        }
    }

    private const string FluentV2 = "FluentV2";
}
