using System.Collections.Immutable;

namespace JsMath.Port;

public sealed record SampledCurve(ImmutableArray<SampledComponent> Components, int EvaluationCount, int VertexCount, bool HasMissingData, bool BudgetExceeded);
