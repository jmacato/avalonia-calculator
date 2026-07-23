using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record CircularSignChart(RootIsolationCertificate Roots, ImmutableArray<int> GapSigns, bool InfinityIsRoot)
{
    public static CircularSignChart Create(RationalFunction function, ResourceBudget budget)
    {
        RootIsolationCertificate roots = SturmRootIsolator.Isolate(function.Numerator, budget);
        var signs = ImmutableArray.CreateBuilder<int>(roots.Roots.Length + 1);
        for (int gap = 0; gap <= roots.Roots.Length; gap++)
        {
            BigRational sample = Sample(roots.Roots, gap);
            signs.Add(function.Numerator.Evaluate(sample, budget).Sign * function.Denominator.Evaluate(sample, budget).Sign);
        }

        int degreeDifference = function.Numerator.Degree - function.Denominator.Degree;
        bool infinityRoot = degreeDifference < 0;
        return new CircularSignChart(roots, signs.MoveToImmutable(), infinityRoot);
    }

    private static BigRational Sample(ImmutableArray<ExactReal> roots, int gap)
    {
        if (roots.IsEmpty)
        {
            return BigRational.Zero;
        }

        if (gap == 0)
        {
            return Lower(roots[0]) - BigRational.One;
        }

        if (gap == roots.Length)
        {
            return Upper(roots[^1]) + BigRational.One;
        }

        return (Upper(roots[gap - 1]) + Lower(roots[gap])) / 2;
    }

    private static BigRational Lower(ExactReal value)
    {
        return value switch
        {
            RationalReal rational => rational.Value,
            AlgebraicReal algebraic => algebraic.IsolatingInterval.Lower,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }

    private static BigRational Upper(ExactReal value)
    {
        return value switch
        {
            RationalReal rational => rational.Value,
            AlgebraicReal algebraic => algebraic.IsolatingInterval.Upper,
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    }
}
