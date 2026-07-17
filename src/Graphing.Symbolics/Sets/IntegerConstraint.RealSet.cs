using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal readonly record struct IntegerConstraint(string Parameter, Comparison Comparison, ExactIntegerConstant Bound)
{
    public static IntegerConstraint All(string parameter) => new(parameter, Comparison.Equal, ExactIntegerConstant.Unbounded);
    public string Canonical => Bound.IsUnbounded ? $"{Parameter}:Z" : $"{Parameter}:{(int)Comparison}:{Bound.Value}";
}
