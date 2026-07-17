using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.Core;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Button surface used by Calculator's WinUI-compatible swipe-item template.
/// </summary>
public sealed class FACommandBarButton : Button
{
    public static readonly StyledProperty<FAIconSource?> IconSourceProperty =
        AvaloniaProperty.Register<FACommandBarButton, FAIconSource?>(nameof(IconSource));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<FACommandBarButton, string?>(nameof(Label));

    public FAIconSource? IconSource
    {
        get => GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(FACommandBarButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);

        if (change.Property == IconSourceProperty)
        {
            PseudoClasses.Set(FASharedPseudoclasses.s_pcIcon, change.NewValue is not null);
        }
        else if (change.Property == LabelProperty)
        {
            PseudoClasses.Set(FASharedPseudoclasses.s_pcLabel, change.NewValue is not null);
        }
    }
}
