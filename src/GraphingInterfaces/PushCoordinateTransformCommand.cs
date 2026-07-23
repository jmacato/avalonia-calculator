namespace Graphing;

public sealed record PushCoordinateTransformCommand(GraphCoordinateTransform Transform) : GraphFrameCommand(GraphCommandKind.PushCoordinateTransform);
