using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal abstract record PolynomialFormula
{
    public abstract string Canonical { get; }
}
