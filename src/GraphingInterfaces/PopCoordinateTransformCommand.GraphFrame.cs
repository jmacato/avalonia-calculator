using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;

public sealed record PopCoordinateTransformCommand() : GraphFrameCommand(GraphCommandKind.PopCoordinateTransform);
