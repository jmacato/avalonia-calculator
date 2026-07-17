using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record ExactRationalExtraction(ExactCoefficientPolynomial Numerator, ExactCoefficientPolynomial Denominator, ImmutableArray<ExactCoefficientPolynomial> DomainExclusions)
{
    public bool HasNonRationalCoefficient => Numerator.HasNonRationalCoefficient || Denominator.HasNonRationalCoefficient || DomainExclusions.Any(static exclusion => exclusion.HasNonRationalCoefficient);
    public string Canonical => $"exact-rational[{Numerator.Canonical}/{Denominator.Canonical};domain={string.Join(',', DomainExclusions.Select(static exclusion => exclusion.Canonical))}]";
}
