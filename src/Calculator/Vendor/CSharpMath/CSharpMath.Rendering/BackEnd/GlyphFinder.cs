using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpMath.Rendering.BackEnd;

public sealed class GlyphFinder : Display.FrontEnd.IGlyphFinder<Fonts, Glyph>
{
    private GlyphFinder()
    {
    }

    // U+25A1 WHITE SQUARE is the conventional missing-ideograph marker.
    public const char GlyphNotFound = '□';

    public static GlyphFinder Instance { get; } = new();

    public Glyph Lookup(Fonts fonts, int codepoint)
    {
        foreach (FontFace font in fonts.Typefaces)
        {
            ushort glyphId = font.FindGlyph(codepoint);
            if (glyphId != 0)
            {
                return new Glyph(font, glyphId);
            }
        }

        return codepoint == GlyphNotFound ? Glyph.Empty : Lookup(fonts, GlyphNotFound);
    }

    public int GetCodepoint(string value, int index) =>
        index + 1 < value.Length &&
        char.IsHighSurrogate(value[index]) &&
        char.IsLowSurrogate(value[index + 1])
            ? char.ConvertToUtf32(value[index], value[index + 1])
            : index > 0 &&
              char.IsHighSurrogate(value[index - 1]) &&
              char.IsLowSurrogate(value[index])
                ? char.ConvertToUtf32(value[index - 1], value[index])
                : value[index];

    public Glyph FindGlyphForCharacterAtIndex(Fonts fonts, int index, string value) =>
        Lookup(fonts, GetCodepoint(value, index));

    public IEnumerable<Glyph> FindGlyphs(Fonts fonts, string value)
    {
        foreach (Rune rune in value.EnumerateRunes())
        {
            yield return Lookup(fonts, rune.Value);
        }
    }

    public bool GlyphIsEmpty(Glyph glyph) => glyph.IsEmpty;

    public Glyph EmptyGlyph => Glyph.Empty;
}
