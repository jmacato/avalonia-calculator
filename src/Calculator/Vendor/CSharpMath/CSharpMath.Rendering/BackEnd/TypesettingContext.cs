namespace CSharpMath.Rendering.BackEnd
{
    public static class TypesettingContext
    {
        public static Display.FrontEnd.TypesettingContext<MathFontSet, Glyph> Instance { get; } =
          new Display.FrontEnd.TypesettingContext<MathFontSet, Glyph>(
             (fonts, size) => new MathFontSet(fonts, size),
             GlyphBoundsProvider.Instance,
             GlyphFinder.Instance,
             MathTable.Instance
           );
    }
}
