namespace Graphing.Symbolics;

internal readonly record struct ExactIntegerConstant(ExactInteger Value, bool IsUnbounded)
{
    public static ExactIntegerConstant Unbounded { get; } = new(default, true);

    public static implicit operator ExactIntegerConstant(int value)
    {
        return new ExactIntegerConstant(value, false);
    }
}
