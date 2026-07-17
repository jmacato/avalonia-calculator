using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;

public sealed record FillPathCommand(GraphPath Path, GraphPaint Paint) : GraphFrameCommand(GraphCommandKind.FillPath);
