using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Reusable geometry source for <see cref="FAPathIcon"/>.
/// </summary>
public sealed class FAPathIconSource : FAIconSource
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        PathIcon.DataProperty.AddOwner<FAPathIconSource>();

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }
}
