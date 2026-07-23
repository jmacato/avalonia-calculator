using Graphing;
using SkiaSharp;

namespace GraphingRaster.Skia;

internal sealed class SkiaGraphFontCacheEntry
{
    public SkiaGraphFontCacheEntry(string family, GraphFontStyle style, float size)
    {
        Family = family;
        Style = style;
        SizeBits = BitConverter.SingleToInt32Bits(size);
        Typeface = SKTypeface.FromFamilyName(
            family,
            style == GraphFontStyle.Italic ? SKFontStyle.Italic : SKFontStyle.Normal) ?? SKTypeface.Default;
        try
        {
            Font = new SKFont(Typeface, size);
        }
        catch
        {
            Typeface.Dispose();
            throw;
        }
    }

    public string Family { get; }
    public GraphFontStyle Style { get; }
    public int SizeBits { get; }
    public SKTypeface Typeface { get; }
    public SKFont Font { get; }

    public bool Matches(string family, GraphFontStyle style, int sizeBits)
    {
        return Style == style && SizeBits == sizeBits && string.Equals(Family, family, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Font.Dispose();
        Typeface.Dispose();
    }
}
