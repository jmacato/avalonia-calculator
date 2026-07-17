using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record PolynomialBoolean(bool Value) : PolynomialFormula
{
    public override string Canonical => Value ? "true" : "false";
}
