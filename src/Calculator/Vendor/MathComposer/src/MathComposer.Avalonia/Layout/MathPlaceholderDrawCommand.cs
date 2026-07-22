using System.Collections.Immutable;
using Avalonia;
using MathComposer.Core;

namespace MathComposer.Avalonia.Layout;

/// <summary>Draws an inferred editing placeholder at one empty row position.</summary>
public sealed record MathPlaceholderDrawCommand(Rect Bounds, MathPosition Position) : MathDrawCommand;
