using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;

public sealed record PushCoordinateTransformCommand(GraphCoordinateTransform Transform) : GraphFrameCommand(GraphCommandKind.PushCoordinateTransform);
