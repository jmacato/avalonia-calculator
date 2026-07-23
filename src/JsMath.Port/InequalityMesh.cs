using System.Collections.Immutable;
using Graphing;

namespace JsMath.Port;

public sealed record InequalityMesh(ImmutableArray<ImmutableArray<GraphPoint>> FilledPolygons, ImmutableArray<ImmutableArray<GraphPoint>> Contours, int EvaluationCount, int VertexCount, bool HasMissingData);
