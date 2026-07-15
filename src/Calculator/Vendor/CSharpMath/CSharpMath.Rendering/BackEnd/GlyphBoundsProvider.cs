using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CSharpMath.Display;
using CSharpMath.Display.FrontEnd;

namespace CSharpMath.Rendering.BackEnd;

public sealed class GlyphBoundsProvider : IGlyphBoundsProvider<Fonts, Glyph>
{
    private GlyphBoundsProvider()
    {
    }

    public static GlyphBoundsProvider Instance { get; } = new();

    public (IEnumerable<float> Advances, float Total) GetAdvancesForGlyphs(
        Fonts font,
        IEnumerable<Glyph> glyphs,
        int glyphCount)
    {
        var advances = new List<float>(glyphCount);
        foreach (Glyph glyph in glyphs)
        {
            advances.Add(glyph.Typeface.GetAdvance(glyph.GlyphId) * font.ScaleFor(glyph.Typeface));
        }

        return (advances, advances.Sum());
    }

    public IEnumerable<RectangleF> GetBoundingRectsForGlyphs(
        Fonts font,
        IEnumerable<Glyph> glyphs,
        int variantCount)
    {
        var rectangles = new List<RectangleF>(variantCount);
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

    public float GetTypographicWidth(Fonts fonts, AttributedGlyphRun<Fonts, Glyph> run) =>
        GetAdvancesForGlyphs(fonts, run.Glyphs, run.GlyphInfos.Count).Total +
        run.GlyphInfos.Sum(glyph => glyph.KernAfterGlyph);
}
