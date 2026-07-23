using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using System.ComponentModel;

namespace FluentAvalonia.UI.Controls;
/// <summary>
/// Represents the base class for an icon UI element.
/// </summary>
[TypeConverter(typeof(IconElementConverter))]
public class FAIconElement : Control
{
    /// <summary>
    /// Defines the <see cref = "Foreground"/> property
    /// </summary>
    public static readonly AttachedProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<FAIconElement>();
    /// <summary>
    /// Gets or sets a brush that describes the foreground color.
    /// </summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty)
        {
            InvalidateVisual();
        }
    }
}
