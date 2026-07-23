namespace Graphing;

public sealed record StrokePathCommand(GraphPath Path, GraphPaint Paint) : GraphFrameCommand(GraphCommandKind.StrokePath);
