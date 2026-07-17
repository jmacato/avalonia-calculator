using Avalonia.Media;
using System.ComponentModel;
using System.Globalization;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Converts WinUI-compatible icon values into reusable icon sources.
/// </summary>
public sealed class IconSourceConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        return sourceType == typeof(string) ||
            sourceType == typeof(FASymbol) ||
            base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value is FASymbol symbol)
        {
            return new FASymbolIconSource { Symbol = symbol };
        }

        if (value is string text)
        {
            if (Enum.TryParse(text, out FASymbol parsedSymbol))
            {
                return new FASymbolIconSource { Symbol = parsedSymbol };
            }

            if (FAPathIcon.IsDataValid(text, out Geometry? geometry))
            {
                return new FAPathIconSource { Data = geometry };
            }

            return new FAFontIconSource { Glyph = text };
        }

        return base.ConvertFrom(context, culture, value);
    }
}
