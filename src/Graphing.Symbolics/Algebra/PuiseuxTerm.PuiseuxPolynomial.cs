using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal readonly record struct PuiseuxTerm(BigRational Exponent, BigRational Coefficient)
{
    public string Canonical => $"{Exponent}:{Coefficient}";
}
