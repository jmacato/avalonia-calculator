
namespace Graphing.Symbolics;

internal static class FixedRationalPowerValue
{
    public static ExactReal Compose(
        ExactReal basis,
        BigRational exponent,
        ResourceBudget budget)
    {
        budget.Charge();
        if (basis is RationalReal rational &&
            TryComposeRational(rational.Value, exponent, budget, out BigRational exact))
        {
            return new RationalReal(exact);
        }

        return new FunctionReal(
            "power",
            [basis, new RationalReal(exponent)]);
    }

    public static bool TryComposeRational(
        BigRational basis,
        BigRational exponent,
        ResourceBudget budget,
        out BigRational result)
    {
        budget.Charge();
        if (basis.Sign < 0 ||
            (basis.IsZero && exponent.Sign < 0) ||
            exponent.IsInteger ||
            exponent.Numerator < int.MinValue ||
            exponent.Numerator > int.MaxValue ||
            exponent.Denominator > int.MaxValue)
        {
            result = default;
            return false;
        }

        if (basis.IsZero)
        {
            result = BigRational.Zero;
            return true;
        }

        if (basis.IsOne)
        {
            result = BigRational.One;
            return true;
        }

        int degree = (int)exponent.Denominator;
        int power = (int)exponent.Numerator;
        if (!CanAttemptExactPower(basis, degree, power))
        {
            result = default;
            return false;
        }

        if (degree == 2)
        {
            if (!BigRational.TrySquareRoot(basis, out BigRational squareRoot))
            {
                result = default;
                return false;
            }

            if (!CanRaiseWithinLimit(squareRoot, power))
            {
                result = default;
                return false;
            }

            result = squareRoot.Pow(power);
            budget.CheckCoefficient(result);
            return true;
        }

        if (!TryExactNonnegativeRoot(basis.Numerator, degree, budget, out ExactInteger numerator) ||
            !TryExactNonnegativeRoot(basis.Denominator, degree, budget, out ExactInteger denominator))
        {
            result = default;
            return false;
        }

        var rooted = new BigRational(numerator, denominator);
        if (!CanRaiseWithinLimit(rooted, power))
        {
            result = default;
            return false;
        }

        result = rooted.Pow(power);
        budget.CheckCoefficient(result);
        return true;
    }

    private static bool CanAttemptExactPower(
        BigRational basis,
        int degree,
        int power)
    {
        if (degree <= 0)
        {
            return false;
        }

        long magnitude = Math.Abs((long)power);
        return CanBeExactRootWithinLimit(basis.Numerator, degree, magnitude) &&
               CanBeExactRootWithinLimit(basis.Denominator, degree, magnitude);
    }

    private static bool CanBeExactRootWithinLimit(
        ExactInteger component,
        int degree,
        long powerMagnitude)
    {
        component = ExactInteger.Abs(component);
        if (component.IsZero || component.IsOne || powerMagnitude == 0)
        {
            return true;
        }

        long bitLength = component.GetBitLength();
        // The smallest nontrivial degree-th power is 2^degree. If the
        // component is smaller, an exact root is impossible and Newton's
        // method must not construct a degree-sized intermediate merely to
        // discover that fact.
        if (degree > bitLength - 1)
        {
            return false;
        }

        long rootBits = (bitLength + degree - 1) / degree;
        return CanPowerPossiblyFit(rootBits, powerMagnitude);
    }

    private static bool CanRaiseWithinLimit(
        BigRational value,
        int power)
    {
        if (value.IsZero || value.Abs().IsOne || power == 0)
        {
            return true;
        }

        long magnitude = Math.Abs((long)power);
        return CanPowerPossiblyFit(
                   value.Numerator.GetBitLength(),
                   magnitude) &&
               CanPowerPossiblyFit(
                   value.Denominator.GetBitLength(),
                   magnitude);
    }

    private static bool CanPowerPossiblyFit(
        long bitLength,
        long powerMagnitude)
    {
        if (bitLength <= 1 || powerMagnitude == 0)
        {
            return true;
        }

        // n with b bits has n^p bit length at least (b - 1)p + 1.
        // If that lower bound fits, the actual allocation is at most twice
        // the coefficient limit; CheckCoefficient validates the exact result.
        return bitLength - 1 <=
               (AnalysisLimits.CoefficientBits - 1L) / powerMagnitude;
    }

    private static bool TryExactNonnegativeRoot(
        ExactInteger value,
        int degree,
        ResourceBudget budget,
        out ExactInteger root)
    {
        if (value.Sign < 0 || degree <= 0)
        {
            root = default;
            return false;
        }

        if (value < 2 || degree == 1)
        {
            root = value;
            return true;
        }

        int bitLength = checked((int)value.GetBitLength());
        int rootBits = checked((bitLength - 1) / degree + 1);
        ExactInteger candidate = ExactInteger.One << rootBits;
        while (true)
        {
            budget.Charge();
            ExactInteger divisor = ExactInteger.Pow(candidate, degree - 1);
            ExactInteger next = ((degree - 1) * candidate + value / divisor) / degree;
            if (next >= candidate)
            {
                root = candidate;
                return ExactInteger.Pow(candidate, degree) == value;
            }

            candidate = next;
        }
    }
}
