using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace CSharpMath.Rendering.BackEnd;

public sealed class Fonts : Display.FrontEnd.IFont<Glyph>
{
    private readonly IReadOnlyList<FontFace> _typefaces;

    public Fonts(IEnumerable<GlyphTypeface> localTypefaces, float pointSize)
        : this(localTypefaces.Select(FontFace.Get).ToArray(), pointSize)
    {
    }

    internal Fonts(Fonts source, float pointSize)
        : this(source._typefaces, pointSize)
    {
    }

    private Fonts(IReadOnlyList<FontFace> typefaces, float pointSize)
    {
        if (typefaces.Count == 0)
        {
            throw new InvalidOperationException(
                "CSharpMath requires at least one Avalonia GlyphTypeface with an OpenType MATH table.");
        }

        _typefaces = typefaces;
        PointSize = pointSize;
        MathTypeface = _typefaces.First(typeface => typeface.HasMathData);
    }

    public float PointSize { get; }

    internal float PixelSize => PointSize * 96f / 72f;

    internal FontFace MathTypeface { get; }

    internal IReadOnlyList<FontFace> Typefaces => _typefaces;

    internal float ScaleFor(FontFace typeface) => PixelSize / typeface.DesignEmHeight;
}
