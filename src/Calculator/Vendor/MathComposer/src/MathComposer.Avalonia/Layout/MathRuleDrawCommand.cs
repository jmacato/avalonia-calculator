using Avalonia;

namespace MathComposer.Avalonia.Layout;

/// <summary>Draws a filled mathematical rule.</summary>
public sealed record MathRuleDrawCommand(Rect Bounds, bool IsError = false) : MathDrawCommand;
