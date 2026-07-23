namespace MathComposer.Avalonia.OpenType;

/// <summary>A ready-made glyph variant and its extension-axis measurement.</summary>
public readonly record struct OpenTypeMathGlyphVariant(ushort GlyphId, ushort AdvanceMeasurement);
