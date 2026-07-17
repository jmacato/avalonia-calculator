using System;

namespace CSharpMath.Rendering.BackEnd;

public readonly record struct Glyph
{
    internal Glyph(FontFace typeface, ushort glyphId)
    {
        Typeface = typeface ?? throw new ArgumentNullException(nameof(typeface));
        GlyphId = glyphId;
    }

    internal FontFace Typeface { get; }

    internal ushort GlyphId { get; }

    public bool IsEmpty => Typeface is null;

    public static readonly Glyph Empty;
}
