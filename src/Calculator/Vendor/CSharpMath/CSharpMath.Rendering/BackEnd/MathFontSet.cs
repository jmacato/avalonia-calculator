using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace CSharpMath.Rendering.BackEnd;

public readonly record struct MathFontSet : Display.FrontEnd.IFont<Glyph>
{
    private readonly FontFace[] _typefaces;

    public MathFontSet(IEnumerable<GlyphTypeface> localTypefaces, float pointSize)
        : this(CreateTypefaces(localTypefaces), pointSize)
    {
    }

    internal MathFontSet(MathFontSet source, float pointSize)
        : this(source._typefaces, pointSize)
    {
    }

    private MathFontSet(FontFace[] typefaces, float pointSize)
    {
        if (typefaces.Length == 0)
        {
            throw new InvalidOperationException(
                "CSharpMath requires at least one Avalonia GlyphTypeface with an OpenType MATH table.");
        }

        _typefaces = typefaces;
        PointSize = pointSize;
        MathTypeface = Array.Find(_typefaces, static typeface => typeface.HasMathData) ??
            throw new InvalidOperationException(
                "CSharpMath requires at least one Avalonia GlyphTypeface with an OpenType MATH table.");
    }

    public float PointSize { get; }

    internal float PixelSize => PointSize * 96f / 72f;

    internal FontFace MathTypeface { get; }

    internal IReadOnlyList<FontFace> Typefaces => _typefaces;

    internal float ScaleFor(FontFace typeface) => PixelSize / typeface.DesignEmHeight;

    internal void Dispose()
    {
        if (_typefaces is null)
        {
            return;
        }

        foreach (FontFace typeface in _typefaces)
        {
            typeface.Dispose();
        }
    }

    private static FontFace[] CreateTypefaces(IEnumerable<GlyphTypeface> localTypefaces)
    {
        ArgumentNullException.ThrowIfNull(localTypefaces);
        return localTypefaces.Select(FontFace.Get).ToArray();
    }
}
