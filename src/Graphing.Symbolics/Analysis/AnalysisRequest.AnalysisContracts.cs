using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record AnalysisRequest(InputExpression Expression, AnalysisFeatures Features, AngleUnit AngleUnit, string Variable, Func<bool> RevisionIsCurrent);
