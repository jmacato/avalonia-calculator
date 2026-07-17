using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using FluentAvalonia.Core;

namespace FluentAvalonia.UI.Controls;

internal static class FAIconHelpers
{
    internal static FAFontIcon CreateFontIconFromFontIconSource(FAFontIconSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var icon = new FAFontIcon
        {
            [!TextElement.FontWeightProperty] = source[!TextElement.FontWeightProperty],
            [!TextElement.FontStyleProperty] = source[!TextElement.FontStyleProperty],
            [!TextElement.FontFamilyProperty] = source[!TextElement.FontFamilyProperty],
            [!TextElement.FontSizeProperty] = source[!TextElement.FontSizeProperty],
            [!FAFontIcon.GlyphProperty] = source[!FAFontIconSource.GlyphProperty]
        };
        BindForeground(icon, source);
        return icon;
    }

    internal static FAPathIcon CreatePathIconFromPathIconSource(FAPathIconSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var icon = new FAPathIcon
        {
            [!PathIcon.DataProperty] = source[!FAPathIconSource.DataProperty]
        };
        BindForeground(icon, source);
        return icon;
    }

    internal static FASymbolIcon CreateSymbolIconFromSymbolIconSource(FASymbolIconSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var icon = new FASymbolIcon
        {
            [!FASymbolIcon.SymbolProperty] = source[!FASymbolIconSource.SymbolProperty],
            [!FASymbolIcon.FontSizeProperty] = source[!FASymbolIconSource.FontSizeProperty]
        };
        BindForeground(icon, source);
        return icon;
    }

    internal static Control? CreateFromUnknown(FAIconSource? source) => source switch
    {
        FAFontIconSource font => CreateFontIconFromFontIconSource(font),
        FAPathIconSource path => CreatePathIconFromPathIconSource(path),
        FASymbolIconSource symbol => CreateSymbolIconFromSymbolIconSource(symbol),
        _ => null
    };

    private static void BindForeground(Control icon, FAIconSource source)
    {
        IObservable<Avalonia.Data.BindingValue<Avalonia.Media.IBrush?>> foreground =
            source.GetBindingObservable(FAIconSource.ForegroundProperty);
        icon.Bind(
            TextElement.ForegroundProperty,
            source.IsSet(FAIconSource.ForegroundProperty) ? foreground : foreground.Skip(1),
            priority: BindingPriority.LocalValue);
    }
}
