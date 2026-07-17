using System;
using System.Collections.Generic;
using CSharpMath.Atom;
using CSharpMath.Atom.Atoms;
using CSharpMath.Display.Displays;
using CSharpMath.Display.FrontEnd;
using InvalidCodePathException = CSharpMath.Structures.InvalidCodePathException;
using System.Drawing;
using System.Linq;
using Range = CSharpMath.Atom.Range;

namespace CSharpMath.Display;

public static class Typesetter
{
    public static ListDisplay<TFont, TGlyph> CreateLine<TFont, TGlyph>(MathList list, TFont font, TypesettingContext<TFont, TGlyph> context, LineStyle style)
        where TFont : IFont<TGlyph> => list is null ? throw new ArgumentNullException(nameof(list)) : Typesetter<TFont, TGlyph>.CreateLine(list.Clone(true), font, context, style, false);
    public static bool UnicodeLengthIsOne(string? str) => str?.Length switch
    {
        1 => true,
        2 when char.IsHighSurrogate(str[0]) && char.IsLowSurrogate(str[1]) => true,
        _ => false
    };
    private static TGlyph FindVariantGlyph<TFont, TGlyph>(FontMathTable<TFont, TGlyph> mathTable, IGlyphBoundsProvider<TFont, TGlyph> boundsProvider, TFont styleFont, TGlyph rawGlyph, float targetWidth, out float glyphAscent, out float glyphDescent, out float glyphWidth)
        where TFont : IFont<TGlyph>
    {
        var (glyphs, nGlyphs) = mathTable.GetHorizontalVariantsForGlyph(rawGlyph);
        if (nGlyphs == 0)
            throw new InvalidCodePathException("Incorrect GetHorizontalVariantsForGlyph implementation. " + "There should always be at least one variant -- the glyph itself");
        var glyphsArray = glyphs as TGlyph[] ?? glyphs.ToArray();
        var boundingBoxes = boundsProvider.GetBoundingRectsForGlyphs(styleFont, glyphsArray, nGlyphs);
        var (advances, _) = boundsProvider.GetAdvancesForGlyphs(styleFont, glyphsArray, nGlyphs);
        TGlyph currentGlyph = default!;
        // These NaN values should never be return ed. We have to set them to keep the compiler happy.
        glyphAscent = float.NaN;
        glyphDescent = float.NaN;
        glyphWidth = float.NaN;
        foreach (var (advance, bounds, glyph) in advances.Zip(boundingBoxes, glyphsArray, ValueTuple.Create))
        {
            bounds.GetAscentDescentWidth(out float ascent, out float descent, out float _);
            var width = bounds.Right;
            if (width > targetWidth)
            {
                if (glyphAscent is not float.NaN)
                    return glyph;
                // glyph dimensions are not yet set
                glyphWidth = advance;
                glyphAscent = ascent;
                glyphDescent = descent;
                return glyph;
            }

            currentGlyph = glyph;
            glyphWidth = advance;
            glyphAscent = ascent;
            glyphDescent = descent;
        }

        return currentGlyph;
    }

    public static GlyphDisplay<TFont, TGlyph> CreateAccentGlyphDisplay<TFont, TGlyph>(ListDisplay<TFont, TGlyph> accentee, TGlyph accenteeSingleGlyph, TGlyph accent, TypesettingContext<TFont, TGlyph> context, TFont styleFont, Range atomRange)
        where TFont : IFont<TGlyph>
    {
        ArgumentNullException.ThrowIfNull(accentee);
        ArgumentNullException.ThrowIfNull(context);
        var accenteeWidth = accentee.Width;
        var accentGlyph = FindVariantGlyph(context.MathTable, context.GlyphBoundsProvider, styleFont, accent, accenteeWidth, out float glyphAscent, out float glyphDescent, out float glyphWidth);
        var delta = Math.Min(accentee.Ascent, context.MathTable.AccentBaseHeight(styleFont));
        float accentAdjustment = context.MathTable.GetTopAccentAdjustment(styleFont, accentGlyph);
        float accenteeAdjustment = context.GlyphFinder.GlyphIsEmpty(accenteeSingleGlyph) ? accenteeWidth / 2 : context.MathTable.GetTopAccentAdjustment(styleFont, accenteeSingleGlyph);
        float skew = accenteeAdjustment - accentAdjustment;
        var height = accentee.Ascent - delta;
        var accentPosition = new PointF(skew, height);
        return new GlyphDisplay<TFont, TGlyph>(accentGlyph, atomRange, styleFont, glyphAscent, glyphDescent, glyphWidth)
        {
            Position = accentPosition
        };
    }
}
