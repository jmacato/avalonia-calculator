using System.Collections.Immutable;
using System.Numerics;

namespace Graphing.Symbolics;

internal readonly record struct RationalInterval(BigRational Lower, BigRational Upper)
{
    public BigRational Midpoint => (Lower + Upper) / 2;

    public BigRational Width => Upper - Lower;
}

internal abstract record ExactReal;

internal sealed record RationalReal(BigRational Value) : ExactReal;

internal sealed record AlgebraicReal(
    UnivariatePolynomial Polynomial,
    RationalInterval IsolatingInterval,
    int RootIndex,
    ImmutableArray<int> ThomEncoding) : ExactReal;

internal sealed record AffinePiReal(
    BigRational PiCoefficient,
    BigRational Constant) : ExactReal;

internal sealed record NamedReal(string Name) : ExactReal;

internal sealed record FunctionReal(string Function, ImmutableArray<ExactReal> Arguments) : ExactReal;

internal sealed record AlgebraicImageReal(
    RationalFunction Function,
    AlgebraicReal Argument) : ExactReal;

internal sealed class SturmChain
{
    private SturmChain(
        UnivariatePolynomial polynomial,
        ImmutableArray<UnivariatePolynomial> sequence)
    {
        Polynomial = polynomial;
        Sequence = sequence;
    }

    public UnivariatePolynomial Polynomial { get; }

    public ImmutableArray<UnivariatePolynomial> Sequence { get; }

    public static SturmChain Create(UnivariatePolynomial polynomial, ResourceBudget budget)
    {
        if (polynomial.IsZero)
        {
            throw new ArgumentException("The zero polynomial has no Sturm chain.", nameof(polynomial));
        }

        UnivariatePolynomial squareFree = polynomial.SquareFreePart(budget).PrimitivePositive(budget);
        var sequence = ImmutableArray.CreateBuilder<UnivariatePolynomial>();
        sequence.Add(squareFree);
        UnivariatePolynomial derivative = squareFree.Derivative(budget);
        if (!derivative.IsZero)
        {
            sequence.Add(derivative);
        }

        while (!derivative.IsZero)
        {
            budget.Charge();
            UnivariatePolynomial previous = sequence[^2];
            (_, UnivariatePolynomial remainder) = previous.Divide(derivative, budget);
            if (remainder.IsZero)
            {
                break;
            }

            derivative = remainder.Negate(budget);
            sequence.Add(derivative);
        }

        return new SturmChain(squareFree, sequence.ToImmutable());
    }

    public int Variations(BigRational point, ResourceBudget budget)
    {
        int variations = 0;
        int previousSign = 0;
        foreach (UnivariatePolynomial polynomial in Sequence)
        {
            int sign = polynomial.Evaluate(point, budget).Sign;
            if (sign == 0)
            {
                continue;
            }

            if (previousSign != 0 && sign != previousSign)
            {
                variations++;
            }

            previousSign = sign;
        }

        return variations;
    }

    public int CountRoots(BigRational lower, BigRational upper, ResourceBudget budget)
    {
        if (lower >= upper)
        {
            return 0;
        }

        if (Polynomial.Evaluate(lower, budget).IsZero || Polynomial.Evaluate(upper, budget).IsZero)
        {
            throw new ArgumentException("Sturm interval endpoints must not be roots.");
        }

        return Variations(lower, budget) - Variations(upper, budget);
    }

    public bool IsValid(ResourceBudget budget)
    {
        if (Sequence.IsEmpty || !Sequence[0].Equals(Polynomial))
        {
            return false;
        }

        if (Sequence.Length == 1)
        {
            return Polynomial.Degree == 0;
        }

        if (!Sequence[1].Equals(Polynomial.Derivative(budget)))
        {
            return false;
        }

        for (int index = 2; index < Sequence.Length; index++)
        {
            (_, UnivariatePolynomial remainder) = Sequence[index - 2].Divide(Sequence[index - 1], budget);
            if (!Sequence[index].Equals(remainder.Negate(budget)))
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed record RootIsolationCertificate(
    UnivariatePolynomial Polynomial,
    ImmutableArray<ExactReal> Roots,
    ImmutableArray<UnivariatePolynomial> SturmSequence);

internal static class SturmRootIsolator
{
    private const int SmallRationalSearchBound = 64;

    public static RootIsolationCertificate Isolate(
        UnivariatePolynomial source,
        ResourceBudget budget)
    {
        if (source.IsZero)
        {
            throw new ArgumentException("The zero polynomial does not have isolated roots.", nameof(source));
        }

        UnivariatePolynomial polynomial = source.SquareFreePart(budget).PrimitivePositive(budget);
        var rationalRoots = ImmutableArray.CreateBuilder<RationalReal>();
        polynomial = ExtractSmallRationalRoots(polynomial, rationalRoots, budget);

        ImmutableArray<AlgebraicReal> algebraicRoots = polynomial.Degree <= 0
            ? []
            : IsolateRemaining(
                polynomial,
                rationalRoots.Select(static root => root.Value).ToImmutableArray(),
                budget);

        var roots = rationalRoots.Cast<ExactReal>().Concat(algebraicRoots).ToList();
        roots.Sort((left, right) => CompareByRationalBounds(left, right));
        SeparateAdjacentRoots(roots, budget);
        SturmChain certificateChain = SturmChain.Create(source, budget);
        return new RootIsolationCertificate(
            source.SquareFreePart(budget).PrimitivePositive(budget),
            roots.ToImmutableArray(),
            certificateChain.Sequence);
    }

    public static bool Verify(RootIsolationCertificate certificate, ResourceBudget budget)
    {
        SturmChain expected = SturmChain.Create(certificate.Polynomial, budget);
        if (certificate.SturmSequence.Length != expected.Sequence.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Sequence.Length; index++)
        {
            if (!certificate.SturmSequence[index].Equals(expected.Sequence[index]))
            {
                return false;
            }
        }

        int accounted = 0;
        BigRational? previousUpper = null;
        foreach (ExactReal root in certificate.Roots)
        {
            switch (root)
            {
                case RationalReal rational:
                    if (!certificate.Polynomial.Evaluate(rational.Value, budget).IsZero)
                    {
                        return false;
                    }

                    if (previousUpper is not null && rational.Value < previousUpper.Value)
                    {
                        return false;
                    }

                    previousUpper = rational.Value;
                    accounted++;
                    break;
                case AlgebraicReal algebraic:
                    if (!algebraic.Polynomial.Equals(certificate.Polynomial) &&
                        !Divides(certificate.Polynomial, algebraic.Polynomial, budget))
                    {
                        return false;
                    }

                    SturmChain chain = SturmChain.Create(algebraic.Polynomial, budget);
                    if (chain.CountRoots(
                            algebraic.IsolatingInterval.Lower,
                            algebraic.IsolatingInterval.Upper,
                            budget) != 1)
                    {
                        return false;
                    }

                    if (previousUpper is not null && algebraic.IsolatingInterval.Lower < previousUpper.Value)
                    {
                        return false;
                    }

                    previousUpper = algebraic.IsolatingInterval.Upper;
                    accounted++;
                    break;
                default:
                    return false;
            }
        }

        if (certificate.Polynomial.Degree <= 0)
        {
            return accounted == 0;
        }

        SturmChain total = SturmChain.Create(certificate.Polynomial, budget);
        (BigRational lower, BigRational upper) = RootBounds(certificate.Polynomial, budget);
        return total.CountRoots(lower, upper, budget) == accounted;
    }

    private static UnivariatePolynomial ExtractSmallRationalRoots(
        UnivariatePolynomial polynomial,
        ImmutableArray<RationalReal>.Builder roots,
        ResourceBudget budget)
    {
        if (polynomial.Degree <= 0)
        {
            return polynomial;
        }

        var candidates = new SortedSet<BigRational>();
        for (int denominator = 1; denominator <= SmallRationalSearchBound; denominator++)
        {
            for (int numerator = -SmallRationalSearchBound; numerator <= SmallRationalSearchBound; numerator++)
            {
                budget.Charge();
                if (BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator).IsOne)
                {
                    candidates.Add(new BigRational(numerator, denominator));
                }
            }
        }

        UnivariatePolynomial current = polynomial;
        foreach (BigRational candidate in candidates)
        {
            if (current.Degree <= 0)
            {
                break;
            }

            if (!current.Evaluate(candidate, budget).IsZero)
            {
                continue;
            }

            roots.Add(new RationalReal(candidate));
            UnivariatePolynomial factor = UnivariatePolynomial.Create([-candidate, BigRational.One], budget);
            (UnivariatePolynomial quotient, UnivariatePolynomial remainder) = current.Divide(factor, budget);
            if (!remainder.IsZero)
            {
                throw new InvalidOperationException("An exact rational root failed synthetic division.");
            }

            current = quotient;
        }

        return current;
    }

    private static ImmutableArray<AlgebraicReal> IsolateRemaining(
        UnivariatePolynomial polynomial,
        ImmutableArray<BigRational> excludedRationalRoots,
        ResourceBudget budget)
    {
        SturmChain chain = SturmChain.Create(polynomial, budget);
        (BigRational lower, BigRational upper) = RootBounds(polynomial, budget);
        var intervals = new List<RationalInterval>();
        BigRational start = lower;
        foreach (BigRational excluded in excludedRationalRoots.Order())
        {
            if (excluded <= lower || excluded >= upper)
            {
                continue;
            }

            IsolateInterval(chain, start, excluded, intervals, budget);
            start = excluded;
        }

        IsolateInterval(chain, start, upper, intervals, budget);
        intervals.Sort(static (left, right) => left.Lower.CompareTo(right.Lower));
        var result = ImmutableArray.CreateBuilder<AlgebraicReal>(intervals.Count);
        for (int index = 0; index < intervals.Count; index++)
        {
            ImmutableArray<int> thom = ThomEncoding(polynomial, intervals[index], budget);
            result.Add(new AlgebraicReal(polynomial, intervals[index], index + 1, thom));
        }

        return result.MoveToImmutable();
    }

    private static void IsolateInterval(
        SturmChain chain,
        BigRational lower,
        BigRational upper,
        List<RationalInterval> result,
        ResourceBudget budget)
    {
        int count = chain.CountRoots(lower, upper, budget);
        if (count == 0)
        {
            return;
        }

        if (count == 1)
        {
            result.Add(new RationalInterval(lower, upper));
            budget.AddCells(1);
            return;
        }

        BigRational midpoint = (lower + upper) / 2;
        if (chain.Polynomial.Evaluate(midpoint, budget).IsZero)
        {
            BigRational width = upper - lower;
            int denominator = 3;
            do
            {
                budget.Charge();
                int numerator = denominator / 2;
                midpoint = lower + (width * new BigRational(numerator, denominator));
                denominator++;
            }
            while (chain.Polynomial.Evaluate(midpoint, budget).IsZero);
        }

        IsolateInterval(chain, lower, midpoint, result, budget);
        IsolateInterval(chain, midpoint, upper, result, budget);
    }

    private static (BigRational Lower, BigRational Upper) RootBounds(
        UnivariatePolynomial polynomial,
        ResourceBudget budget)
    {
        BigRational maximum = BigRational.Zero;
        BigRational leading = polynomial.LeadingCoefficient.Abs();
        for (int degree = 0; degree < polynomial.Degree; degree++)
        {
            BigRational ratio = polynomial[degree].Abs() / leading;
            if (ratio > maximum)
            {
                maximum = ratio;
            }
        }

        BigInteger integerBound = maximum.Ceiling() + BigInteger.One;
        BigRational upper = new(integerBound);
        while (polynomial.Evaluate(upper, budget).IsZero)
        {
            upper += BigRational.One;
        }

        BigRational lower = -upper;
        while (polynomial.Evaluate(lower, budget).IsZero)
        {
            lower -= BigRational.One;
        }

        return (lower, upper);
    }

    private static ImmutableArray<int> ThomEncoding(
        UnivariatePolynomial polynomial,
        RationalInterval interval,
        ResourceBudget budget)
    {
        var signs = ImmutableArray.CreateBuilder<int>();
        UnivariatePolynomial derivative = polynomial;
        for (int order = 1; order <= polynomial.Degree; order++)
        {
            derivative = derivative.Derivative(budget);
            signs.Add(SignAtIsolatedRoot(polynomial, derivative, interval, budget));
        }

        return signs.ToImmutable();
    }

    internal static int SignAtIsolatedRoot(
        UnivariatePolynomial rootPolynomial,
        UnivariatePolynomial valuePolynomial,
        RationalInterval interval,
        ResourceBudget budget)
    {
        UnivariatePolynomial gcd = UnivariatePolynomial.GreatestCommonDivisor(
            rootPolynomial,
            valuePolynomial,
            budget);
        if (gcd.Degree > 0)
        {
            SturmChain gcdChain = SturmChain.Create(gcd, budget);
            if (gcdChain.CountRoots(interval.Lower, interval.Upper, budget) == 1)
            {
                return 0;
            }
        }

        if (valuePolynomial.IsConstant)
        {
            return valuePolynomial.ConstantCoefficient.Sign;
        }

        RationalInterval current = interval;
        SturmChain rootChain = SturmChain.Create(rootPolynomial, budget);
        SturmChain valueChain = SturmChain.Create(valuePolynomial, budget);
        while (true)
        {
            BigRational lowerValue = valuePolynomial.Evaluate(current.Lower, budget);
            BigRational upperValue = valuePolynomial.Evaluate(current.Upper, budget);
            if (lowerValue.Sign != 0 &&
                upperValue.Sign != 0 &&
                valueChain.CountRoots(current.Lower, current.Upper, budget) == 0)
            {
                return lowerValue.Sign;
            }

            BigRational midpoint = current.Midpoint;
            if (rootPolynomial.Evaluate(midpoint, budget).IsZero)
            {
                return valuePolynomial.Evaluate(midpoint, budget).Sign;
            }

            int leftCount = rootChain.CountRoots(current.Lower, midpoint, budget);
            current = leftCount == 1
                ? new RationalInterval(current.Lower, midpoint)
                : new RationalInterval(midpoint, current.Upper);
        }
    }

    private static int CompareByRationalBounds(ExactReal left, ExactReal right)
    {
        BigRational leftBound = left switch
        {
            RationalReal rational => rational.Value,
            AlgebraicReal algebraic => algebraic.IsolatingInterval.Lower,
            _ => throw new ArgumentOutOfRangeException(nameof(left))
        };
        BigRational rightBound = right switch
        {
            RationalReal rational => rational.Value,
            AlgebraicReal algebraic => algebraic.IsolatingInterval.Lower,
            _ => throw new ArgumentOutOfRangeException(nameof(right))
        };
        int comparison = leftBound.CompareTo(rightBound);
        if (comparison != 0)
        {
            return comparison;
        }

        return (left, right) switch
        {
            (RationalReal, AlgebraicReal) => -1,
            (AlgebraicReal, RationalReal) => 1,
            _ => 0
        };
    }

    private static void SeparateAdjacentRoots(List<ExactReal> roots, ResourceBudget budget)
    {
        for (int index = 1; index < roots.Count; index++)
        {
            while (UpperBound(roots[index - 1]) >= LowerBound(roots[index]))
            {
                budget.Charge();
                if (roots[index - 1] is AlgebraicReal left)
                {
                    roots[index - 1] = left with
                    {
                        IsolatingInterval = Refine(left.Polynomial, left.IsolatingInterval, budget)
                    };
                }

                if (roots[index] is AlgebraicReal right)
                {
                    roots[index] = right with
                    {
                        IsolatingInterval = Refine(right.Polynomial, right.IsolatingInterval, budget)
                    };
                }

                if (roots[index - 1] is RationalReal && roots[index] is RationalReal)
                {
                    throw new InvalidOperationException("Distinct exact rational roots were not ordered.");
                }
            }
        }
    }

    internal static RationalInterval Refine(
        UnivariatePolynomial polynomial,
        RationalInterval interval,
        ResourceBudget budget)
    {
        SturmChain chain = SturmChain.Create(polynomial, budget);
        BigRational midpoint = interval.Midpoint;
        if (polynomial.Evaluate(midpoint, budget).IsZero)
        {
            BigRational quarterWidth = interval.Width / 4;
            return new RationalInterval(midpoint - quarterWidth, midpoint + quarterWidth);
        }

        return chain.CountRoots(interval.Lower, midpoint, budget) == 1
            ? new RationalInterval(interval.Lower, midpoint)
            : new RationalInterval(midpoint, interval.Upper);
    }

    private static BigRational LowerBound(ExactReal value) => value switch
    {
        RationalReal rational => rational.Value,
        AlgebraicReal algebraic => algebraic.IsolatingInterval.Lower,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static BigRational UpperBound(ExactReal value) => value switch
    {
        RationalReal rational => rational.Value,
        AlgebraicReal algebraic => algebraic.IsolatingInterval.Upper,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static bool Divides(
        UnivariatePolynomial source,
        UnivariatePolynomial divisor,
        ResourceBudget budget)
    {
        if (divisor.IsZero)
        {
            return false;
        }

        (_, UnivariatePolynomial remainder) = source.Divide(divisor, budget);
        return remainder.IsZero;
    }
}
