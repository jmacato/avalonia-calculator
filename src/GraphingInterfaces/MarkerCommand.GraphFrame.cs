using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;

public sealed record MarkerCommand(GraphPoint Center, float Radius, GraphMarkerShape Shape, GraphPaint Fill, GraphPaint? Stroke = null) : GraphFrameCommand(GraphCommandKind.Marker);
