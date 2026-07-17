using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record ConstantYFeaturePoint(RealFamily X, ExactReal Y) : FeaturePoint;
