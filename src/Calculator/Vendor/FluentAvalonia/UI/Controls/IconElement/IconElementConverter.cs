using Avalonia.Media;
using System.ComponentModel;
using System.Globalization;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Converts WinUI-compatible icon values into renderable icon elements.
/// </summary>
public sealed class IconElementConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        ArgumentNullException.ThrowIfNull(sourceType);
        return sourceType == typeof(string) ||
            sourceType == typeof(FASymbol) ||
            sourceType == typeof(FAIconSource) ||
            base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value is FASymbol symbol)
        {
            return new FASymbolIcon { Symbol = symbol };
        }

        if (value is FAIconSource source)
        {
            return FAIconHelpers.CreateFromUnknown(source);
        }

        if (value is string text)
        {
            if (Enum.TryParse(text, out FASymbol parsedSymbol))
            {
                return new FASymbolIcon { Symbol = parsedSymbol };
            }

            if (FAPathIcon.IsDataValid(text, out Geometry? geometry))
            {
                return new FAPathIcon { Data = geometry };
            }

            return new FAFontIcon { Glyph = text };
        }

        return base.ConvertFrom(context, culture, value);
    }
}
