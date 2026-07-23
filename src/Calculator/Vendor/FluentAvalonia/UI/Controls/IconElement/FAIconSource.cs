using Avalonia;
using Avalonia.Media;
using System.ComponentModel;

namespace FluentAvalonia.UI.Controls;
/// <summary>
/// Represents the base class for an icon source
/// </summary>
[TypeConverter(typeof(IconSourceConverter))]
public abstract class FAIconSource : AvaloniaObject
{
    /// <summary>
    /// Defines the <see cref = "Foreground"/> property
    /// </summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<FAIconSource, IBrush?>(nameof(Foreground));
    /// <summary>
    /// Gets or sets a brush that describes the foreground color.
    /// </summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
}
