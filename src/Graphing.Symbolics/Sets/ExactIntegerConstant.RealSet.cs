using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal readonly record struct ExactIntegerConstant(ExactInteger Value, bool IsUnbounded)
{
    public static ExactIntegerConstant Unbounded { get; } = new(default, true);

    public static implicit operator ExactIntegerConstant(int value) => new(value, false);
}
