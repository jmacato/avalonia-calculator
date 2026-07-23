using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class ExactCoefficientMath
{
    public static bool TryAdd(
        ExactScalar left,
        ExactScalar right,
        ResourceBudget budget,
        out ExactScalar result)
    {
        return ExactScalar.TryAdd(left, right, budget, out result);
    }

    public static bool TrySubtract(
        ExactScalar left,
        ExactScalar right,
        ResourceBudget budget,
        out ExactScalar result)
    {
        return ExactScalar.TryAdd(left, right.Negate(), budget, out result);
    }

    public static ExactScalar Divide(
        ExactScalar numerator,
        ExactScalar denominator,
        ResourceBudget budget)
    {
        return numerator.Multiply(denominator.Reciprocal(budget), budget);
    }

    public static bool TryDiscriminant(
        ExactCoefficientPolynomial polynomial,
        ResourceBudget budget,
        out ExactScalar discriminant)
    {
        if (polynomial.Degree != 2)
        {
            discriminant = default;
            return false;
        }

        ExactScalar bSquared = polynomial[1].Multiply(polynomial[1], budget);
        ExactScalar fourAc = polynomial[2]
            .Multiply(polynomial[0], budget)
            .Multiply(ExactScalar.FromRational(new BigRational(4)), budget);
        return TrySubtract(bSquared, fourAc, budget, out discriminant);
    }

    public static bool TryDeterminant(
        ExactCoefficientPolynomial numerator,
        ExactCoefficientPolynomial denominator,
        ResourceBudget budget,
        out ExactScalar determinant)
    {
        if (numerator.Degree > 1 || denominator.Degree != 1)
        {
            determinant = default;
            return false;
        }

        ExactScalar ad = numerator[1].Multiply(denominator[0], budget);
        ExactScalar bc = numerator[0].Multiply(denominator[1], budget);
        return TrySubtract(ad, bc, budget, out determinant);
    }

    public static bool TrySolveRoots(
        ExactCoefficientPolynomial polynomial,
        ResourceBudget budget,
        out ImmutableArray<ExactScalar> roots)
    {
        budget.Charge();
        if (polynomial.IsZero)
        {
            roots = default;
            return false;
        }

        if (polynomial.IsConstant)
        {
            roots = [];
            return true;
        }

        if (polynomial.Degree == 1)
        {
            roots = [Divide(polynomial[0].Negate(), polynomial[1], budget)];
            return true;
        }

        if (polynomial.Degree != 2 ||
            !TryDiscriminant(polynomial, budget, out ExactScalar discriminant))
        {
            roots = default;
            return false;
        }

        if (discriminant.Sign < 0)
        {
            roots = [];
            return true;
        }

        ExactScalar twoA = polynomial[2].Multiply(
            ExactScalar.FromRational(new BigRational(2)),
            budget);
        if (polynomial[1].IsZero)
        {
            ExactScalar radicand = Divide(polynomial[0].Negate(), polynomial[2], budget);
            if (!radicand.TrySquareRoot(budget, out ExactScalar root))
            {
                roots = default;
                return false;
            }

            roots = root.IsZero ? [root] : [root.Negate(), root];
            return true;
        }

        if (!discriminant.TrySquareRoot(budget, out ExactScalar squareRoot) ||
            !TryAdd(polynomial[1].Negate(), squareRoot.Negate(), budget, out ExactScalar lowerNumerator) ||
            !TryAdd(polynomial[1].Negate(), squareRoot, budget, out ExactScalar upperNumerator))
        {
            roots = default;
            return false;
        }

        ExactScalar first = Divide(lowerNumerator, twoA, budget);
        if (squareRoot.IsZero)
        {
            roots = [first];
            return true;
        }

        ExactScalar second = Divide(upperNumerator, twoA, budget);
        roots = [first, second];
        return true;
    }

    public static bool TryVertex(
        ExactCoefficientPolynomial polynomial,
        ResourceBudget budget,
        out ExactScalar x,
        out ExactScalar y)
    {
        if (polynomial.Degree != 2)
        {
            x = default;
            y = default;
            return false;
        }

        ExactScalar twoA = polynomial[2].Multiply(
            ExactScalar.FromRational(new BigRational(2)),
            budget);
        x = Divide(polynomial[1].Negate(), twoA, budget);
        return polynomial.TryEvaluate(x, budget, out y);
    }

    public static bool SameValue(ExactScalar left, ExactScalar right)
    {
        return string.Equals(left.Canonical, right.Canonical, StringComparison.Ordinal);
    }

    public static ExactReal AddValues(
        ExactScalar left,
        ExactScalar right,
        ResourceBudget budget)
    {
        if (!TryAdd(left, right, budget, out ExactScalar result))
        {
            throw new InvalidOperationException("The exact sum did not retain proved order evidence.");
        }

        return result.Value;
    }
}
