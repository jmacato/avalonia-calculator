using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;

public sealed record StrokePathCommand(GraphPath Path, GraphPaint Paint) : GraphFrameCommand(GraphCommandKind.StrokePath);
