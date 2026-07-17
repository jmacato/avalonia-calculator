using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace FluentAvalonia.Converters;

/// <summary>
/// Converter that converts a color to a SolidColorBrush
/// </summary>
public class FAColorToBrushConv : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Color c)
            return new SolidColorBrush(c);

        return BindingOperations.DoNothing;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ISolidColorBrush sc)
            return sc.Color;

        return BindingOperations.DoNothing;
    }
}
