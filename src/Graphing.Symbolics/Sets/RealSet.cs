using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;

internal enum BoundKind
{
    NegativeInfinity,
    Finite,
    PositiveInfinity
}

internal readonly record struct RealBound(BoundKind Kind, ExactReal? Value)
{
    public static RealBound NegativeInfinity { get; } = new(BoundKind.NegativeInfinity, null);

    public static RealBound PositiveInfinity { get; } = new(BoundKind.PositiveInfinity, null);

    public static RealBound Finite(ExactReal value) => new(BoundKind.Finite, value);

    public string Canonical => Kind switch
    {
        BoundKind.NegativeInfinity => "-inf",
        BoundKind.PositiveInfinity => "+inf",
        BoundKind.Finite => ExactRealCanonical.Format(Value!),
        _ => throw new ArgumentOutOfRangeException()
    };
}

internal readonly record struct IntegerConstraint(
    string Parameter,
    Comparison Comparison,
    BigIntegerConstant Bound)
{
    public static IntegerConstraint All(string parameter) =>
        new(parameter, Comparison.Equal, BigIntegerConstant.Unbounded);

    public string Canonical => Bound.IsUnbounded
        ? $"{Parameter}:Z"
        : $"{Parameter}:{(int)Comparison}:{Bound.Value}";
}

internal readonly record struct BigIntegerConstant(System.Numerics.BigInteger Value, bool IsUnbounded)
{
    public static BigIntegerConstant Unbounded { get; } = new(default, true);

    public static implicit operator BigIntegerConstant(int value) => new(value, false);
}

internal abstract record RealSet
{
    public abstract string Canonical { get; }

    public virtual bool IsEmpty => false;
}

internal sealed record EmptySet : RealSet
{
    public static EmptySet Instance { get; } = new();

    private EmptySet()
    {
    }

    public override string Canonical => "empty";

    public override bool IsEmpty => true;
}

internal sealed record AllRealSet : RealSet
{
    public static AllRealSet Instance { get; } = new();

    private AllRealSet()
    {
    }

    public override string Canonical => "reals";
}

internal sealed record PointSet(ImmutableArray<ExactReal> Points) : RealSet
{
    public override string Canonical =>
        $"points[{string.Join(',', Points.Select(ExactRealCanonical.Format))}]";
}

internal sealed record IntervalSet(
    RealBound Lower,
    bool IncludesLower,
    RealBound Upper,
    bool IncludesUpper) : RealSet
{
    public override string Canonical =>
        $"interval[{Lower.Canonical},{(IncludesLower ? 1 : 0)},{Upper.Canonical},{(IncludesUpper ? 1 : 0)}]";
}

internal sealed record UnionSet(ImmutableArray<RealSet> Operands) : RealSet
{
    public override string Canonical =>
        $"union[{string.Join(',', Operands.Select(static operand => operand.Canonical))}]";
}

internal sealed record IntersectionSet(ImmutableArray<RealSet> Operands) : RealSet
{
    public override string Canonical =>
        $"intersection[{string.Join(',', Operands.Select(static operand => operand.Canonical))}]";
}

internal sealed record DifferenceSet(RealSet Source, RealSet Removed) : RealSet
{
    public override string Canonical => $"difference[{Source.Canonical},{Removed.Canonical}]";
}

internal readonly record struct PeriodicInterval(
    ExactReal LowerOffset,
    bool IncludesLower,
    ExactReal UpperOffset,
    bool IncludesUpper)
{
    public string Canonical =>
        $"[{ExactRealCanonical.Format(LowerOffset)},{(IncludesLower ? 1 : 0)},{ExactRealCanonical.Format(UpperOffset)},{(IncludesUpper ? 1 : 0)}]";
}

internal sealed record PeriodicIntervalSet(
    ExactReal Period,
    string Parameter,
    IntegerConstraint Constraint,
    ImmutableArray<PeriodicInterval> Intervals) : RealSet
{
    public override string Canonical =>
        $"periodic-intervals[{ExactRealCanonical.Format(Period)},{Parameter},{Constraint.Canonical},{string.Join(',', Intervals.Select(static interval => interval.Canonical))}]";
}

internal sealed record PeriodicPointSet(
    ExactReal Offset,
    ExactReal Period,
    string Parameter,
    IntegerConstraint Constraint) : RealSet
{
    public override string Canonical =>
        $"periodic-points[{ExactRealCanonical.Format(Offset)},{ExactRealCanonical.Format(Period)},{Parameter},{Constraint.Canonical}]";
}

internal sealed record IntegerLatticeSet(
    string Expression,
    ImmutableArray<string> Parameters,
    ImmutableArray<string> Predicates) : RealSet
{
    public override string Canonical =>
        $"integer-lattice[{Expression};{string.Join(',', Parameters)};{string.Join(',', Predicates)}]";
}

internal sealed record IsolatedRootsSet(RootIsolationCertificate Isolation) : RealSet
{
    public override string Canonical =>
        $"isolated-roots[{Isolation.Polynomial.Canonical};{string.Join(',', Isolation.Roots.Select(ExactRealCanonical.Format))}]";
}

internal sealed record ComprehensionSet(
    string Variable,
    string PredicateCanonical) : RealSet
{
    public override string Canonical => $"comprehension[{Variable};{PredicateCanonical}]";
}

internal static class RealSets
{
    public static RealSet Union(params RealSet[] sets) => Union(sets.AsEnumerable());

    public static RealSet Union(IEnumerable<RealSet> sets)
    {
        var operands = new SortedDictionary<string, RealSet>(StringComparer.Ordinal);
        foreach (RealSet set in sets)
        {
            if (set is AllRealSet)
            {
                return AllRealSet.Instance;
            }

            if (set is EmptySet)
            {
                continue;
            }

            if (set is UnionSet union)
            {
                foreach (RealSet child in union.Operands)
                {
                    operands[child.Canonical] = child;
                }
            }
            else
            {
                operands[set.Canonical] = set;
            }
        }

        return operands.Count switch
        {
            0 => EmptySet.Instance,
            1 => operands.Values.First(),
            _ => new UnionSet(operands.Values.ToImmutableArray())
        };
    }

    public static RealSet Intersection(params RealSet[] sets)
    {
        var operands = new SortedDictionary<string, RealSet>(StringComparer.Ordinal);
        foreach (RealSet set in sets)
        {
            if (set is EmptySet)
            {
                return EmptySet.Instance;
            }

            if (set is AllRealSet)
            {
                continue;
            }

            if (set is IntersectionSet intersection)
            {
                foreach (RealSet child in intersection.Operands)
                {
                    operands[child.Canonical] = child;
                }
            }
            else
            {
                operands[set.Canonical] = set;
            }
        }

        return operands.Count switch
        {
            0 => AllRealSet.Instance,
            1 => operands.Values.First(),
            _ => new IntersectionSet(operands.Values.ToImmutableArray())
        };
    }

    public static RealSet Points(IEnumerable<ExactReal> points)
    {
        ImmutableArray<ExactReal> materialized = points
            .DistinctBy(ExactRealCanonical.Format)
            .OrderBy(ExactRealCanonical.SortKey, StringComparer.Ordinal)
            .ToImmutableArray();
        return materialized.IsEmpty ? EmptySet.Instance : new PointSet(materialized);
    }
}

internal static class ExactRealCanonical
{
    public static string Format(ExactReal value) => value switch
    {
        RationalReal rational => "q:" + rational.Value,
        AlgebraicReal algebraic =>
            $"alg:{algebraic.Polynomial.Canonical}:{algebraic.IsolatingInterval.Lower}:{algebraic.IsolatingInterval.Upper}:{algebraic.RootIndex}:{string.Join(',', algebraic.ThomEncoding)}",
        AffinePiReal affinePi => $"pi:{affinePi.PiCoefficient}:{affinePi.Constant}",
        NamedReal named => "named:" + named.Name,
        FunctionReal function =>
            $"fn:{function.Function}({string.Join(',', function.Arguments.Select(Format))})",
        AlgebraicImageReal image =>
            $"image:{image.Function.Numerator.Canonical}/{image.Function.Denominator.Canonical}@{Format(image.Argument)}",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    public static string SortKey(ExactReal value) => value switch
    {
        RationalReal rational => "0:" + rational.Value,
        AlgebraicReal algebraic => "1:" + algebraic.IsolatingInterval.Lower,
        _ => "2:" + Format(value)
    };
}
