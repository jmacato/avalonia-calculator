using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record FourierPolynomial(ImmutableSortedDictionary<int, GaussianRational> Coefficients)
{
    public bool IsConstant => Coefficients.Keys.All(static frequency => frequency == 0);

    public int FrequencyGcd
    {
        get
        {
            int gcd = 0;
            foreach (int frequency in Coefficients.Keys)
            {
                gcd = (int)ExactInteger.GreatestCommonDivisor(gcd, Math.Abs(frequency));
            }

            return gcd;
        }
    }
}
