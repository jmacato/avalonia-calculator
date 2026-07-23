namespace Graphing.Symbolics;
/// <summary>
/// Proves order relations for supported exact scalars by rational enclosure.
/// Bounds are propagated with outward exact rational arithmetic; no binary
/// floating-point value participates in a proof.
/// </summary>
internal static class ExactScalarOrder
{
    private const int SquareRootRefinements = 256;
    private const int SquareRootCacheCapacity = 64;
    private static readonly ExactScalarOrderSquareRootCacheEntry?[] s_squareRootCache = new ExactScalarOrderSquareRootCacheEntry[SquareRootCacheCapacity];
    public static bool TryCompareAbsolute(ExactScalar scalar, BigRational nonnegative, ResourceBudget budget, out int comparison)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(nonnegative.Sign);
        ExactScalar absolute = scalar.Abs();
        if (absolute.RationalValue is { } rational)
        {
            comparison = rational.CompareTo(nonnegative);
            return true;
        }

        if (nonnegative.IsZero)
        {
            comparison = absolute.IsZero ? 0 : 1;
            return true;
        }

        if (!TryEnclose(absolute.Value, budget, out RationalEnclosure enclosure))
        {
            comparison = default;
            return false;
        }

        if (enclosure.Lower > nonnegative)
        {
            comparison = 1;
            return true;
        }

        if (enclosure.Upper < nonnegative)
        {
            comparison = -1;
            return true;
        }

        if (enclosure.IsExact && enclosure.Lower == nonnegative)
        {
            comparison = 0;
            return true;
        }

        comparison = default;
        return false;
    }

    internal static bool TryEnclose(ExactReal value, ResourceBudget budget, out RationalEnclosure enclosure)
    {
        budget.Charge();
        switch (value)
        {
            case RationalReal rational:
                budget.CheckCoefficient(rational.Value);
                enclosure = new RationalEnclosure(rational.Value, rational.Value);
                return true;
            case AffinePiReal affine:
                RationalEnclosure pi = PiBounds(budget);
                enclosure = Add(Scale(pi, affine.PiCoefficient, budget), Exact(affine.Constant, budget), budget);
                return true;
            case NamedReal { Name: "e" }:
                enclosure = EulerBounds(budget);
                return true;
            case FunctionReal function:
                return TryEncloseFunction(function, budget, out enclosure);
            default:
                enclosure = default;
                return false;
        }
    }

    private static bool TryEncloseFunction(FunctionReal function, ResourceBudget budget, out RationalEnclosure enclosure)
    {
        return function.Function switch
        {
            "negate" => TryUnary(function, budget, Negate, out enclosure),
            "scale" => TryScale(function, budget, out enclosure),
            "add" => TryBinary(function, budget, Add, out enclosure),
            "multiply" => TryBinary(function, budget, Multiply, out enclosure),
            "divide" => TryDivide(function, budget, out enclosure),
            "power" => TryPower(function, budget, out enclosure),
            "sqrt" => TrySquareRoot(function, budget, out enclosure),
            _ => Fail(out enclosure)
        };
    }

    private static bool TryUnary(FunctionReal function, ResourceBudget budget, Func<RationalEnclosure, ResourceBudget, RationalEnclosure> operation, out RationalEnclosure enclosure)
    {
        if (function.Arguments.Length != 1 || !TryEnclose(function.Arguments[0], budget, out RationalEnclosure argument))
        {
            return Fail(out enclosure);
        }

        enclosure = operation(argument, budget);
        return true;
    }

    private static bool TryScale(FunctionReal function, ResourceBudget budget, out RationalEnclosure enclosure)
    {
        if (function.Arguments is not [var value, RationalReal factor] || !TryEnclose(value, budget, out RationalEnclosure argument))
        {
            return Fail(out enclosure);
        }

        enclosure = Scale(argument, factor.Value, budget);
        return true;
    }

    private static bool TryBinary(FunctionReal function, ResourceBudget budget, Func<RationalEnclosure, RationalEnclosure, ResourceBudget, RationalEnclosure> operation, out RationalEnclosure enclosure)
    {
        if (function.Arguments.Length != 2 || !TryEnclose(function.Arguments[0], budget, out RationalEnclosure left) || !TryEnclose(function.Arguments[1], budget, out RationalEnclosure right))
        {
            return Fail(out enclosure);
        }

        enclosure = operation(left, right, budget);
        return true;
    }

    private static bool TryDivide(FunctionReal function, ResourceBudget budget, out RationalEnclosure enclosure)
    {
        if (function.Arguments.Length != 2 || !TryEnclose(function.Arguments[0], budget, out RationalEnclosure numerator) || !TryEnclose(function.Arguments[1], budget, out RationalEnclosure denominator) || denominator.Lower <= BigRational.Zero && denominator.Upper >= BigRational.Zero)
        {
            return Fail(out enclosure);
        }

        enclosure = Multiply(numerator, Reciprocal(denominator, budget), budget);
        return true;
    }

    private static bool TryPower(FunctionReal function, ResourceBudget budget, out RationalEnclosure enclosure)
    {
        if (function.Arguments is not [var basis, RationalReal exponent] || !exponent.Value.IsInteger || exponent.Value.Numerator < int.MinValue || exponent.Value.Numerator > int.MaxValue || !TryEnclose(basis, budget, out RationalEnclosure basisBounds))
        {
            return Fail(out enclosure);
        }

        return TryIntegerPower(basisBounds, (int)exponent.Value.Numerator, budget, out enclosure);
    }

    private static bool TrySquareRoot(FunctionReal function, ResourceBudget budget, out RationalEnclosure enclosure)
    {
        if (function.Arguments.Length != 1 || !TryEnclose(function.Arguments[0], budget, out RationalEnclosure radicand) || radicand.Upper.Sign < 0)
        {
            return Fail(out enclosure);
        }

        BigRational lower = radicand.Lower.Sign < 0 ? BigRational.Zero : radicand.Lower;
        RationalEnclosure lowerRoot = EncloseSquareRoot(lower, budget);
        RationalEnclosure upperRoot = EncloseSquareRoot(radicand.Upper, budget);
        enclosure = Checked(new RationalEnclosure(lowerRoot.Lower, upperRoot.Upper), budget);
        return true;
    }

    private static bool TryIntegerPower(RationalEnclosure basis, int exponent, ResourceBudget budget, out RationalEnclosure enclosure)
    {
        if (exponent == 0)
        {
            enclosure = Exact(BigRational.One, budget);
            return true;
        }

        if (exponent < 0)
        {
            if (exponent == int.MinValue || basis.Lower <= BigRational.Zero && basis.Upper >= BigRational.Zero || !TryIntegerPower(basis, -exponent, budget, out RationalEnclosure positive))
            {
                return Fail(out enclosure);
            }

            enclosure = Reciprocal(positive, budget);
            return true;
        }

        PreflightPower(basis, exponent);
        BigRational lowerPower = basis.Lower.Pow(exponent);
        BigRational upperPower = basis.Upper.Pow(exponent);
        if ((exponent & 1) != 0)
        {
            enclosure = Checked(new RationalEnclosure(lowerPower, upperPower), budget);
            return true;
        }

        BigRational upper = Max(lowerPower, upperPower);
        BigRational lower = basis.Lower <= BigRational.Zero && basis.Upper >= BigRational.Zero ? BigRational.Zero : Min(lowerPower, upperPower);
        enclosure = Checked(new RationalEnclosure(lower, upper), budget);
        return true;
    }

    private static RationalEnclosure PiBounds(ResourceBudget budget)
    {
        // Machin: pi/4 = 4 atan(1/5) - atan(1/239). Alternating-series
        // remainders enclose each arctangent by consecutive partial sums.
        RationalEnclosure atanFive = AtanReciprocalBounds(5, 32, budget);
        RationalEnclosure atan239 = AtanReciprocalBounds(239, 8, budget);
        RationalEnclosure difference = Add(Scale(atanFive, new BigRational(4), budget), Negate(atan239, budget), budget);
        return Scale(difference, new BigRational(4), budget);
    }

    private static RationalEnclosure AtanReciprocalBounds(int denominator, int terms, ResourceBudget budget)
    {
        var x = new BigRational(ExactInteger.One, new ExactInteger(denominator));
        BigRational square = x * x;
        BigRational power = x;
        BigRational partial = BigRational.Zero;
        for (int index = 0; index < terms; index++)
        {
            budget.Charge();
            BigRational term = power / new BigRational(2 * index + 1);
            partial += (index & 1) == 0 ? term : -term;
            budget.CheckCoefficient(partial);
            power *= square;
            budget.CheckCoefficient(power);
        }

        BigRational next = power / new BigRational(2 * terms + 1);
        budget.CheckCoefficient(next);
        // The callers deliberately request an even number of terms, so the
        // partial sum ends below atan(x) and the next positive term is above.
        if ((terms & 1) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(terms));
        }

        return Checked(new RationalEnclosure(partial, partial + next), budget);
    }

    private static RationalEnclosure EulerBounds(ResourceBudget budget)
    {
        // e = sum 1/n!. After the Nth term, the positive tail is strictly
        // below 1/(N*N!) for N >= 1.
        const int terms = 32;
        BigRational term = BigRational.One;
        BigRational partial = BigRational.One;
        for (int index = 1; index <= terms; index++)
        {
            budget.Charge();
            term /= new BigRational(index);
            budget.CheckCoefficient(term);
            partial += term;
            budget.CheckCoefficient(partial);
        }

        BigRational remainderBound = term / new BigRational(terms);
        return Checked(new RationalEnclosure(partial, partial + remainderBound), budget);
    }

    private static RationalEnclosure EncloseSquareRoot(BigRational value, ResourceBudget budget)
    {
        if (value.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (BigRational.TrySquareRoot(value, out BigRational exact))
        {
            return Exact(exact, budget);
        }

        int cacheIndex = (value.GetHashCode() & int.MaxValue) % SquareRootCacheCapacity;
        ExactScalarOrderSquareRootCacheEntry? cached = Volatile.Read(ref s_squareRootCache[cacheIndex]);
        if (cached is not null && cached.Value == value)
        {
            // Preserve the same deterministic work charge as the refinement
            // path even when the immutable enclosure can be reused.
            budget.Charge(SquareRootRefinements * 3L);
            return Checked(cached.Enclosure, budget);
        }

        BigRational lower = BigRational.Zero;
        BigRational upper = value > BigRational.One ? value : BigRational.One;
        for (int iteration = 0; iteration < SquareRootRefinements; iteration++)
        {
            budget.Charge();
            BigRational midpoint = (lower + upper) / new BigRational(2);
            budget.CheckCoefficient(midpoint);
            BigRational square = midpoint * midpoint;
            budget.CheckCoefficient(square);
            if (square < value)
            {
                lower = midpoint;
            }
            else
            {
                upper = midpoint;
            }
        }

        RationalEnclosure enclosure = Checked(new RationalEnclosure(lower, upper), budget);
        Interlocked.Exchange(ref s_squareRootCache[cacheIndex], new ExactScalarOrderSquareRootCacheEntry(value, enclosure));
        return enclosure;
    }

    private static RationalEnclosure Exact(BigRational value, ResourceBudget budget)
    {
        return Checked(new RationalEnclosure(value, value), budget);
    }

    private static RationalEnclosure Add(RationalEnclosure left, RationalEnclosure right, ResourceBudget budget)
    {
        return Checked(new RationalEnclosure(left.Lower + right.Lower, left.Upper + right.Upper), budget);
    }

    private static RationalEnclosure Negate(RationalEnclosure value, ResourceBudget budget)
    {
        return Checked(new RationalEnclosure(-value.Upper, -value.Lower), budget);
    }

    private static RationalEnclosure Scale(RationalEnclosure value, BigRational factor, ResourceBudget budget)
    {
        return factor.Sign < 0
            ? Checked(new RationalEnclosure(value.Upper * factor, value.Lower * factor), budget)
            : Checked(new RationalEnclosure(value.Lower * factor, value.Upper * factor), budget);
    }

    private static RationalEnclosure Multiply(RationalEnclosure left, RationalEnclosure right, ResourceBudget budget)
    {
        BigRational a = left.Lower * right.Lower;
        BigRational b = left.Lower * right.Upper;
        BigRational c = left.Upper * right.Lower;
        BigRational d = left.Upper * right.Upper;
        return Checked(new RationalEnclosure(Min(Min(a, b), Min(c, d)), Max(Max(a, b), Max(c, d))), budget);
    }

    private static RationalEnclosure Reciprocal(RationalEnclosure value, ResourceBudget budget)
    {
        if (value.Lower <= BigRational.Zero && value.Upper >= BigRational.Zero)
        {
            throw new DivideByZeroException();
        }

        return Checked(new RationalEnclosure(value.Upper.Reciprocal(), value.Lower.Reciprocal()), budget);
    }

    private static RationalEnclosure Checked(RationalEnclosure enclosure, ResourceBudget budget)
    {
        budget.CheckCoefficient(enclosure.Lower);
        budget.CheckCoefficient(enclosure.Upper);
        if (enclosure.Lower > enclosure.Upper)
        {
            throw new InvalidOperationException($"An exact scalar enclosure was inverted: {enclosure.Lower} > {enclosure.Upper}.");
        }

        return enclosure;
    }

    private static void PreflightPower(RationalEnclosure basis, int exponent)
    {
        CheckPowerEndpoint(basis.Lower, exponent);
        CheckPowerEndpoint(basis.Upper, exponent);
    }

    private static void CheckPowerEndpoint(BigRational value, int exponent)
    {
        if (value.IsZero || value.Abs().IsOne || exponent == 0)
        {
            return;
        }

        long magnitude = Math.Abs((long)exponent);
        long guaranteedBits = Math.Max(checked((value.Numerator.GetBitLength() - 1) * magnitude + 1), checked((value.Denominator.GetBitLength() - 1) * magnitude + 1));
        if (guaranteedBits > AnalysisLimits.CoefficientBits)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.CoefficientBits));
        }
    }

    private static bool Fail(out RationalEnclosure enclosure)
    {
        enclosure = default;
        return false;
    }

    private static BigRational Min(BigRational left, BigRational right)
    {
        return left <= right ? left : right;
    }

    private static BigRational Max(BigRational left, BigRational right)
    {
        return left >= right ? left : right;
    }
}
