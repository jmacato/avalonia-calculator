using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal sealed record BooleanFormula(bool Value) : Formula
{
    public override string Canonical => Value ? "true" : "false";
}
