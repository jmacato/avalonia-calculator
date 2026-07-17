using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal sealed record PolynomialPhasePreimage(string PhaseCanonical, ImmutableArray<PuiseuxTerm> PhaseTerms, string Variable, BigRational CoordinateOffset, UnivariatePolynomial ParameterPolynomial, int SubstitutionDegree, bool ParameterIsNonnegative, bool BoundaryIncluded, ExactReal TargetOffset, ExactReal TargetPeriod, string Parameter, IntegerConstraint Constraint)
{
    public string PhaseDisplay => PuiseuxPolynomial.Format(PhaseTerms, CoordinateDisplay, signedOddRoots: !ParameterIsNonnegative);
    public string CoordinateDisplay => CoordinateOffset.IsZero ? Variable : CoordinateOffset.Sign < 0 ? $"({Variable} + {CoordinateOffset.Abs()})" : $"({Variable} − {CoordinateOffset})";
    public string Canonical => $"phase-preimage[{PhaseCanonical};terms[{string.Join(',', PhaseTerms.Select(static term => term.Canonical))}];" + $"{Variable};{CoordinateOffset};{ParameterPolynomial.Canonical};{SubstitutionDegree};" + $"{(ParameterIsNonnegative ? 1 : 0)};{(BoundaryIncluded ? 1 : 0)};" + $"{ExactRealCanonical.Format(TargetOffset)};{ExactRealCanonical.Format(TargetPeriod)};" + $"{Parameter};{Constraint.Canonical}]";
}
