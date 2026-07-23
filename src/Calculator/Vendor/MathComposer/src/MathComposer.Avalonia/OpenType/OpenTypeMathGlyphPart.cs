namespace MathComposer.Avalonia.OpenType;

/// <summary>One part of a stretchy glyph assembly.</summary>
public readonly record struct OpenTypeMathGlyphPart(
    ushort GlyphId,
    ushort StartConnectorLength,
    ushort EndConnectorLength,
    ushort FullAdvance,
    bool IsExtender);
