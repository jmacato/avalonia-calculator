using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record ExactRationalPattern(ExactCoefficientPatternKind RationalKind, ExactCoefficientPolynomial Numerator, ExactCoefficientPolynomial Denominator, ImmutableArray<ExactCoefficientPolynomial> DomainExclusions) : ExactCoefficientPattern(RationalKind)
{
    public override string Canonical => $"{(int)Kind}:{Numerator.Canonical}/{Denominator.Canonical};domain={string.Join(',', DomainExclusions.Select(static exclusion => exclusion.Canonical))}";
    public override ImmutableArray<ExactScalar> BaseScalars => Numerator.Coefficients.Concat(Denominator.Coefficients).Concat(DomainExclusions.SelectMany(static exclusion => exclusion.Coefficients)).DistinctBy(static scalar => scalar.Canonical).OrderBy(static scalar => scalar.Canonical, StringComparer.Ordinal).ToImmutableArray();
}
