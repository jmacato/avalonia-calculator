namespace Graphing;

public sealed record FillPathCommand(GraphPath Path, GraphPaint Paint) : GraphFrameCommand(GraphCommandKind.FillPath);
