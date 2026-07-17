using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal sealed record ComparisonFormula(ValueTerm Left, Comparison Comparison, ValueTerm Right) : Formula
{
    public override string Canonical => $"cmp({(int)Comparison},{Left.Canonical},{Right.Canonical})";
}
