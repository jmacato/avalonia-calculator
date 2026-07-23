using Graphing;

namespace GraphingImpl;

internal readonly record struct ManagedGraphRendererClosestCandidate(bool Available, int EquationIndex, double DistanceSquared, GraphPoint Screen, GraphPoint User, double Parameter)
{
    public static ManagedGraphRendererClosestCandidate None { get; } = new(false, -1, double.PositiveInfinity, new GraphPoint(double.NaN, double.NaN), new GraphPoint(double.NaN, double.NaN), double.NaN);
}
