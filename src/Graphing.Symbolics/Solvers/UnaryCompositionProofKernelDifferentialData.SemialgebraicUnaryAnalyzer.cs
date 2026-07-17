using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record UnaryCompositionProofKernelDifferentialData(RationalFunction First, RationalFunction Curvature);
