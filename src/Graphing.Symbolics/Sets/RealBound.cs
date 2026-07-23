namespace Graphing.Symbolics;

internal readonly record struct RealBound(BoundKind Kind, ExactReal? Value)
{
    public static RealBound NegativeInfinity { get; } = new(BoundKind.NegativeInfinity, null);
    public static RealBound PositiveInfinity { get; } = new(BoundKind.PositiveInfinity, null);

    public static RealBound Finite(ExactReal value)
    {
        return new RealBound(BoundKind.Finite, value);
    }

    public string Canonical => Kind switch
    {
        BoundKind.NegativeInfinity => "-inf",
        BoundKind.PositiveInfinity => "+inf",
        BoundKind.Finite => ExactRealCanonical.Format(Value!),
        _ => throw new ArgumentOutOfRangeException()
    };
}
