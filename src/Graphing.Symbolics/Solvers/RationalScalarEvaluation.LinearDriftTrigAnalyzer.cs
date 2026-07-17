using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal readonly record struct RationalScalarEvaluation(bool IsRational, BigRational Value);
