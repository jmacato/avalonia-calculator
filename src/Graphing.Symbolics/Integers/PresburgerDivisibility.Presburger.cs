using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record PresburgerDivisibility(ExactInteger Divisor, LinearIntegerExpression Expression) : PresburgerFormula;
