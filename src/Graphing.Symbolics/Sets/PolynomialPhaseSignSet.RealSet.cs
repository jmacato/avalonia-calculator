using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal sealed record PolynomialPhaseSignSet(string PhaseCanonical, ImmutableArray<PuiseuxTerm> PhaseTerms, string Variable, BigRational CoordinateOffset, RealSet Domain, AngleUnit AngleUnit, string FactorFunction, int FactorSign, Comparison Comparison) : RealSet
{
    public string PhaseDisplay => PuiseuxPolynomial.Format(PhaseTerms, CoordinateOffset.IsZero ? Variable : CoordinateOffset.Sign < 0 ? $"({Variable} + {CoordinateOffset.Abs()})" : $"({Variable} − {CoordinateOffset})", signedOddRoots: Domain is AllRealSet);
    public override string Canonical => $"phase-sign[{PhaseCanonical};terms[{string.Join(',', PhaseTerms.Select(static term => term.Canonical))}];" + $"{Variable};{CoordinateOffset};{Domain.Canonical};{FactorSign}*{FactorFunction};" + $"{(int)AngleUnit};{(int)Comparison}]";
}
