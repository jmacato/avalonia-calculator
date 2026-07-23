namespace MathComposer.Avalonia.OpenType;

/// <summary>Horizontal advance and side-bearing values in font design units.</summary>
public readonly record struct OpenTypeGlyphMetrics(ushort AdvanceWidth, short LeftSideBearing);
