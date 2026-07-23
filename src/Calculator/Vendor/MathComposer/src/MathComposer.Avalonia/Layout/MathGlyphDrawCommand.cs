using Avalonia;

namespace MathComposer.Avalonia.Layout;

/// <summary>Draws one font glyph by glyph ID at a baseline origin.</summary>
public sealed record MathGlyphDrawCommand(
    ushort GlyphId,
    Point BaselineOrigin,
    double FontSize) : MathDrawCommand;
