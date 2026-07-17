using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record SignChart(ImmutableArray<UnivariatePolynomial> Polynomials, RootIsolationCertificate Roots, ImmutableArray<BigRational> GapSamples, ImmutableArray<ImmutableArray<int>> GapSigns, ImmutableArray<ImmutableArray<int>> PointSigns, ImmutableArray<bool> GapDomain, ImmutableArray<bool> PointDomain)
{
    public int SignInGap(UnivariatePolynomial polynomial, int gap) => GapSigns[gap][IndexOf(polynomial)];
    public int SignAtPoint(UnivariatePolynomial polynomial, int point) => PointSigns[point][IndexOf(polynomial)];
    private int IndexOf(UnivariatePolynomial polynomial)
    {
        string canonical = polynomial.Canonical;
        for (int index = 0; index < Polynomials.Length; index++)
        {
            if (string.Equals(Polynomials[index].Canonical, canonical, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new InvalidOperationException("The requested sign polynomial is not in the chart.");
    }
}
