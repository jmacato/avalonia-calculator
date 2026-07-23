namespace MathComposer.Avalonia.OpenType;

/// <summary>A conservative glyph rectangle in font design units.</summary>
public readonly record struct OpenTypeGlyphBounds(short XMin, short YMin, short XMax, short YMax);
