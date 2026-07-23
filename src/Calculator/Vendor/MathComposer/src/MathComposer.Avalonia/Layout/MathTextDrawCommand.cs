using Avalonia;

namespace MathComposer.Avalonia.Layout;

/// <summary>Draws a shaped text run at a baseline origin.</summary>
public sealed record MathTextDrawCommand(
    string Text,
    Point BaselineOrigin,
    double FontSize,
    bool IsError = false) : MathDrawCommand;
