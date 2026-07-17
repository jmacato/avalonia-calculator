using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record ExactRealSubstitutionArithmeticLinearTerm(ExactReal Basis, BigRational Coefficient);
