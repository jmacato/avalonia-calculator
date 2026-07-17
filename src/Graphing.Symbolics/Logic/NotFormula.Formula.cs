using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal sealed record NotFormula(Formula Operand) : Formula
{
    public override string Canonical => $"not({Operand.Canonical})";
}
