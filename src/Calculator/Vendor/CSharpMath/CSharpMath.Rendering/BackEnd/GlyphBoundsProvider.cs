using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CSharpMath.Display;
using CSharpMath.Display.FrontEnd;

namespace CSharpMath.Rendering.BackEnd;

public sealed class GlyphBoundsProvider : IGlyphBoundsProvider<MathFontSet, Glyph>
{
    private GlyphBoundsProvider()
    {
    }

    public static GlyphBoundsProvider Instance { get; } = new();

    public (IEnumerable<float> Advances, float Total) GetAdvancesForGlyphs(
        MathFontSet font,
        IEnumerable<Glyph> glyphs,
        int nGlyphs)
    {
        System.ArgumentNullException.ThrowIfNull(glyphs);
        var advances = new List<float>(nGlyphs);
        foreach (Glyph glyph in glyphs)
        {
            advances.Add(glyph.Typeface.GetAdvance(glyph.GlyphId) * font.ScaleFor(glyph.Typeface));
        }

        return (advances, advances.Sum());
    }

    public IEnumerable<RectangleF> GetBoundingRectsForGlyphs(
        MathFontSet font,
        IEnumerable<Glyph> glyphs,
        int nGlyphs)
    {
        System.ArgumentNullException.ThrowIfNull(glyphs);
        var rectangles = new List<RectangleF>(nGlyphs);
        foreach (Glyph glyph in glyphs)
        {
            Avalonia.Rect bounds = glyph.Typeface.GetInkBounds(glyph.GlyphId);
            float scale = font.ScaleFor(glyph.Typeface);
            rectangles.Add(RectangleF.FromLTRB(
                (float)bounds.Left * scale,
                (float)-bounds.Bottom * scale,
                (float)bounds.Right * scale,
                (float)-bounds.Top * scale));
        }

        return rectangles;
    }

    public float GetTypographicWidth(MathFontSet fonts, AttributedGlyphRun<MathFontSet, Glyph> run)
    {
        System.ArgumentNullException.ThrowIfNull(run);
        return GetAdvancesForGlyphs(fonts, run.Glyphs, run.GlyphInfos.Count).Total +
            run.GlyphInfos.Sum(glyph => glyph.KernAfterGlyph);
    }
}
