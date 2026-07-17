using System;
using System.Collections.Generic;
using System.Text;

namespace CSharpMath.Rendering.BackEnd;

public sealed class GlyphFinder : Display.FrontEnd.IGlyphFinder<MathFontSet, Glyph>
{
    private GlyphFinder()
    {
    }

    // U+25A1 WHITE SQUARE is the conventional missing-ideograph marker.
    public const char GlyphNotFound = '□';

    public static GlyphFinder Instance { get; } = new();

    private static Glyph Lookup(MathFontSet fontSet, int codepoint)
    {
        foreach (FontFace font in fontSet.Typefaces)
        {
            ushort glyphId = font.FindGlyph(codepoint);
            if (glyphId != 0)
            {
                return new Glyph(font, glyphId);
            }
        }

        return codepoint == GlyphNotFound ? Glyph.Empty : Lookup(fontSet, GlyphNotFound);
    }

    private static int GetCodepoint(string text, int index) =>
        index + 1 < text.Length &&
        char.IsHighSurrogate(text[index]) &&
        char.IsLowSurrogate(text[index + 1])
            ? char.ConvertToUtf32(text[index], text[index + 1])
            : index > 0 &&
              char.IsHighSurrogate(text[index - 1]) &&
              char.IsLowSurrogate(text[index])
                ? char.ConvertToUtf32(text[index - 1], text[index])
                : text[index];

    public Glyph FindGlyphForCharacterAtIndex(MathFontSet font, int index, string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        return Lookup(font, GetCodepoint(str, index));
    }

    public IEnumerable<Glyph> FindGlyphs(MathFontSet font, string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        foreach (Rune rune in str.EnumerateRunes())
        {
            yield return Lookup(font, rune.Value);
        }
    }

    public bool GlyphIsEmpty(Glyph glyph) => glyph.IsEmpty;

    public Glyph EmptyGlyph => Glyph.Empty;
}
