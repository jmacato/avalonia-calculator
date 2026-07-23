namespace Graphing;
/// <summary>
/// A compact line primitive for transient grid and axis furniture. It avoids
/// allocating a GraphPath and immutable point storage for every two-point line.
/// </summary>
public sealed record StrokeLineCommand(GraphPoint Start, GraphPoint End, GraphPaint Paint) : GraphFrameCommand(GraphCommandKind.StrokeLine);
