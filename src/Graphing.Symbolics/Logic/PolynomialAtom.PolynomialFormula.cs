using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record PolynomialAtom(UnivariatePolynomial Polynomial, Comparison Comparison) : PolynomialFormula
{
    public override string Canonical => $"atom[{Polynomial.Canonical},{(int)Comparison}]";
}
