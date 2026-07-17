using System.Buffers;
using System.Collections.Immutable;
using Graphing;

namespace JsMath.Port;

internal readonly record struct MarchingSquaresBoundarySegment(GraphPoint A, GraphPoint B);
