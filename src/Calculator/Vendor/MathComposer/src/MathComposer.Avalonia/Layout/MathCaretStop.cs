using System.Collections.Immutable;
using Avalonia;
using MathComposer.Core;

namespace MathComposer.Avalonia.Layout;

/// <summary>A legal caret stop and its hit-test rectangle.</summary>
public readonly record struct MathCaretStop(MathPosition Position, Rect Bounds);
