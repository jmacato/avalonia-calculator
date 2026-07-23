using System.Globalization;
using Graphing.Symbolics;

namespace GraphingImpl;
/// <summary>
/// Converts a small, proved fragment of certified algebraic reals to radicals.
/// Failure is deliberately benign: callers retain the isolating-root descriptor.
/// </summary>
internal static class AlgebraicDisplaySimplifier
{
    private const int MaxSquareFactorTrial = 100_000;
    private const int MaxCubeFactorTrial = 100_000;
    public static bool TryFormat(AlgebraicReal value, out string display)
    {
        if (TryCreateRootForm(value, out AlgebraicDisplaySimplifierRootForm form))
        {
            display = FormatRoot(form);
            return true;
        }

        if (TryFormatCardano(value, out display))
        {
            return true;
        }

        display = string.Empty;
        return false;
    }

    public static bool TryFormat(AlgebraicImageReal value, out string display)
    {
        if (!TryCreateRootForm(value.Argument, out AlgebraicDisplaySimplifierRootForm form))
        {
            display = string.Empty;
            return false;
        }

        if (form.IsDirect)
        {
            AlgebraicDisplaySimplifierQuadraticValue numerator = Evaluate(value.Function.Numerator, form.DirectValue);
            AlgebraicDisplaySimplifierQuadraticValue denominator = Evaluate(value.Function.Denominator, form.DirectValue);
            if (!AlgebraicDisplaySimplifierQuadraticValue.TryDivide(numerator, denominator, out AlgebraicDisplaySimplifierQuadraticValue quotient))
            {
                display = string.Empty;
                return false;
            }

            display = FormatQuadratic(quotient);
            return true;
        }

        AlgebraicDisplaySimplifierExtensionValue extensionNumerator = EvaluateExtension(value.Function.Numerator, form.Square);
        AlgebraicDisplaySimplifierExtensionValue extensionDenominator = EvaluateExtension(value.Function.Denominator, form.Square);
        if (!AlgebraicDisplaySimplifierExtensionValue.TryDivide(extensionNumerator, extensionDenominator, form.Square, out AlgebraicDisplaySimplifierExtensionValue extensionQuotient))
        {
            display = string.Empty;
            return false;
        }

        display = FormatExtension(extensionQuotient, form.Square, form.RootSign);
        return true;
    }

    private static bool TryFormatCardano(AlgebraicReal value, out string display)
    {
        UnivariatePolynomial polynomial = value.Polynomial;
        if (polynomial.Degree != 3 || !IntervalBracketsCubicRoot(value))
        {
            display = string.Empty;
            return false;
        }

        BigRational a = polynomial[3];
        BigRational b = polynomial[2];
        BigRational c = polynomial[1];
        BigRational d = polynomial[0];
        BigRational p = (new BigRational(3) * a * c - b * b) / (new BigRational(3) * a * a);
        BigRational q = (new BigRational(2) * b * b * b - new BigRational(9) * a * b * c + new BigRational(27) * a * a * d) / (new BigRational(27) * a * a * a);
        BigRational discriminant = q * q / 4 + p * p * p / 27;
        if (discriminant.Sign < 0 || !TrySquareRoot(discriminant, out AlgebraicDisplaySimplifierQuadraticValue discriminantRoot))
        {
            display = string.Empty;
            return false;
        }

        BigRational halfNegativeQ = -q / 2;
        AlgebraicDisplaySimplifierQuadraticValue firstRadicand = AlgebraicDisplaySimplifierQuadraticValue.Rational(halfNegativeQ).Add(discriminantRoot);
        AlgebraicDisplaySimplifierQuadraticValue secondRadicand = AlgebraicDisplaySimplifierQuadraticValue.Rational(halfNegativeQ).Subtract(discriminantRoot);
        if (!TryCreateCubeRootTerm(firstRadicand, out AlgebraicDisplaySimplifierCubeRootTerm first) || !TryCreateCubeRootTerm(secondRadicand, out AlgebraicDisplaySimplifierCubeRootTerm second))
        {
            display = string.Empty;
            return false;
        }

        BigRational shift = -b / (new BigRational(3) * a);
        display = FormatCardanoSum(shift, first, second);
        return true;
    }

    private static bool IntervalBracketsCubicRoot(AlgebraicReal value)
    {
        BigRational lower = EvaluateRational(value.Polynomial, value.IsolatingInterval.Lower);
        BigRational upper = EvaluateRational(value.Polynomial, value.IsolatingInterval.Upper);
        return lower.Sign != 0 && upper.Sign != 0 && lower.Sign != upper.Sign;
    }

    private static BigRational EvaluateRational(UnivariatePolynomial polynomial, BigRational argument)
    {
        BigRational result = BigRational.Zero;
        for (int degree = polynomial.Degree; degree >= 0; degree--)
        {
            result = result * argument + polynomial[degree];
        }

        return result;
    }

    private static bool TryCreateCubeRootTerm(AlgebraicDisplaySimplifierQuadraticValue value, out AlgebraicDisplaySimplifierCubeRootTerm term)
    {
        if (value.IsZero)
        {
            term = AlgebraicDisplaySimplifierCubeRootTerm.Rational(BigRational.Zero);
            return true;
        }

        int sign = value.Sign;
        AlgebraicDisplaySimplifierQuadraticValue magnitude = sign < 0 ? value.Negate() : value;
        if (magnitude.IsRational)
        {
            term = CreateRationalCubeRootTerm(magnitude.RationalPart, sign);
            return true;
        }

        ExactInteger denominator = LeastCommonMultiple(magnitude.RationalPart.Denominator, magnitude.RadicalCoefficient.Denominator);
        ExactInteger rationalInteger = magnitude.RationalPart.Numerator * (denominator / magnitude.RationalPart.Denominator);
        ExactInteger radicalInteger = magnitude.RadicalCoefficient.Numerator * (denominator / magnitude.RadicalCoefficient.Denominator);
        ExactInteger numeratorContent = ExactInteger.GreatestCommonDivisor(ExactInteger.Abs(rationalInteger), ExactInteger.Abs(radicalInteger));
        (ExactInteger numeratorOutside, _) = ExtractCubeFactor(numeratorContent);
        (ExactInteger denominatorOutside, _) = ExtractCubeFactor(denominator);
        ExactInteger numeratorCube = numeratorOutside * numeratorOutside * numeratorOutside;
        ExactInteger denominatorCube = denominatorOutside * denominatorOutside * denominatorOutside;
        rationalInteger /= numeratorCube;
        radicalInteger /= numeratorCube;
        denominator /= denominatorCube;
        BigRational coefficient = new BigRational(numeratorOutside, denominatorOutside) * sign;
        string numerator = FormatQuadratic(AlgebraicDisplaySimplifierQuadraticValue.Create(new BigRational(rationalInteger), new BigRational(radicalInteger), magnitude.Radicand));
        string argument = denominator.IsOne ? numerator : $"({numerator})/{denominator.ToString(CultureInfo.InvariantCulture)}";
        term = AlgebraicDisplaySimplifierCubeRootTerm.Radical(coefficient, argument);
        return true;
    }

    private static AlgebraicDisplaySimplifierCubeRootTerm CreateRationalCubeRootTerm(BigRational magnitude, int sign)
    {
        (ExactInteger numeratorOutside, ExactInteger numeratorInside) = ExtractCubeFactor(magnitude.Numerator);
        (ExactInteger denominatorOutside, ExactInteger denominatorInside) = ExtractCubeFactor(magnitude.Denominator);
        BigRational coefficient = new BigRational(numeratorOutside, denominatorOutside) * sign;
        if (numeratorInside.IsOne && denominatorInside.IsOne)
        {
            return AlgebraicDisplaySimplifierCubeRootTerm.Rational(coefficient);
        }

        string argument = denominatorInside.IsOne ? numeratorInside.ToString(CultureInfo.InvariantCulture) : numeratorInside.ToString(CultureInfo.InvariantCulture) + "/" + denominatorInside.ToString(CultureInfo.InvariantCulture);
        return AlgebraicDisplaySimplifierCubeRootTerm.Radical(coefficient, argument);
    }

    private static string FormatCardanoSum(BigRational shift, AlgebraicDisplaySimplifierCubeRootTerm first, AlgebraicDisplaySimplifierCubeRootTerm second)
    {
        BigRational rational = shift + first.RationalValue + second.RationalValue;
        var radicals = new List<AlgebraicDisplaySimplifierCubeRootTerm>(2);
        AddCubeRootTerm(radicals, first);
        AddCubeRootTerm(radicals, second);
        radicals.RemoveAll(static term => term.Coefficient.IsZero);
        if (radicals.Count == 0)
        {
            return Rational(rational);
        }

        if (radicals.Count == 1)
        {
            return FormatOneCubeRootAndRational(radicals[0], rational);
        }

        var parts = new List<(int Sign, string Magnitude)>(radicals.Count + 1);
        parts.AddRange(radicals.Select(static term => (term.Coefficient.Sign, FormatCubeRootMagnitude(term.Coefficient.Abs(), term.Argument!))));
        if (!rational.IsZero)
        {
            parts.Add((rational.Sign, Rational(rational.Abs())));
        }

        return FormatSignedParts(parts);
    }

    private static void AddCubeRootTerm(List<AlgebraicDisplaySimplifierCubeRootTerm> terms, AlgebraicDisplaySimplifierCubeRootTerm candidate)
    {
        if (!candidate.IsRadical)
        {
            return;
        }

        int existing = terms.FindIndex(term => string.Equals(term.Argument, candidate.Argument, StringComparison.Ordinal));
        if (existing < 0)
        {
            terms.Add(candidate);
            return;
        }

        terms[existing] = AlgebraicDisplaySimplifierCubeRootTerm.Radical(terms[existing].Coefficient + candidate.Coefficient, candidate.Argument!);
    }

    private static string FormatOneCubeRootAndRational(AlgebraicDisplaySimplifierCubeRootTerm radical, BigRational rational)
    {
        string magnitude = FormatCubeRootMagnitude(radical.Coefficient.Abs(), radical.Argument!);
        if (rational.IsZero)
        {
            return radical.Coefficient.Sign < 0 ? "−" + magnitude : magnitude;
        }

        if (radical.Coefficient.Sign < 0)
        {
            return rational.Sign < 0 ? $"−{magnitude} − {Rational(rational.Abs())}" : $"{Rational(rational)} − {magnitude}";
        }

        return rational.Sign < 0 ? $"{magnitude} − {Rational(rational.Abs())}" : $"{magnitude} + {Rational(rational)}";
    }

    private static string FormatSignedParts(List<(int Sign, string Magnitude)> parts)
    {
        var builder = new System.Text.StringBuilder();
        for (int index = 0; index < parts.Count; index++)
        {
            (int sign, string magnitude) = parts[index];
            if (index == 0)
            {
                if (sign < 0)
                {
                    builder.Append('−');
                }
            }
            else
            {
                builder.Append(sign < 0 ? " − " : " + ");
            }

            builder.Append(magnitude);
        }

        return builder.ToString();
    }

    private static string FormatCubeRootMagnitude(BigRational coefficient, string argument)
    {
        string root = $"root({argument}, 3)";
        string numerator = coefficient.Numerator.IsOne ? root : coefficient.Numerator.ToString(CultureInfo.InvariantCulture) + root;
        return coefficient.Denominator.IsOne ? numerator : numerator + "/" + coefficient.Denominator.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryCreateRootForm(AlgebraicReal value, out AlgebraicDisplaySimplifierRootForm form)
    {
        if (value.Polynomial.Degree == 2 && TryQuadraticRoots(value.Polynomial, out AlgebraicDisplaySimplifierQuadraticValue first, out AlgebraicDisplaySimplifierQuadraticValue second))
        {
            bool firstMatches = IsInside(first, value.IsolatingInterval);
            bool secondMatches = IsInside(second, value.IsolatingInterval);
            if (firstMatches != secondMatches)
            {
                form = AlgebraicDisplaySimplifierRootForm.Direct(firstMatches ? first : second);
                return true;
            }
        }

        if (value.Polynomial.Degree == 4 && value.Polynomial[1].IsZero && value.Polynomial[3].IsZero && TryBiquadraticRoot(value, out form))
        {
            return true;
        }

        form = default;
        return false;
    }

    private static bool TryBiquadraticRoot(AlgebraicReal value, out AlgebraicDisplaySimplifierRootForm form)
    {
        UnivariatePolynomial polynomial = value.Polynomial;
        if (!TryQuadraticRoots(polynomial[4], polynomial[2], polynomial[0], out AlgebraicDisplaySimplifierQuadraticValue firstSquare, out AlgebraicDisplaySimplifierQuadraticValue secondSquare))
        {
            form = default;
            return false;
        }

        AlgebraicDisplaySimplifierRootForm? match = null;
        foreach (AlgebraicDisplaySimplifierQuadraticValue square in Distinct(firstSquare, secondSquare))
        {
            if (square.Sign <= 0)
            {
                continue;
            }

            foreach (int sign in new[]
            {
                -1,
                1
            }

            )
            {
                if (!IsSignedSquareRootInside(square, sign, value.IsolatingInterval))
                {
                    continue;
                }

                AlgebraicDisplaySimplifierRootForm candidate;
                if (TryPositiveSquareRootInField(square, out AlgebraicDisplaySimplifierQuadraticValue positiveRoot))
                {
                    candidate = AlgebraicDisplaySimplifierRootForm.Direct(sign < 0 ? positiveRoot.Negate() : positiveRoot);
                }
                else
                {
                    candidate = AlgebraicDisplaySimplifierRootForm.Extension(square, sign);
                }

                if (match is not null)
                {
                    form = default;
                    return false;
                }

                match = candidate;
            }
        }

        if (match is null)
        {
            form = default;
            return false;
        }

        form = match.Value;
        return true;
    }

    private static IEnumerable<AlgebraicDisplaySimplifierQuadraticValue> Distinct(AlgebraicDisplaySimplifierQuadraticValue first, AlgebraicDisplaySimplifierQuadraticValue second)
    {
        yield return first;
        if (second != first)
        {
            yield return second;
        }
    }

    private static bool TryQuadraticRoots(UnivariatePolynomial polynomial, out AlgebraicDisplaySimplifierQuadraticValue first, out AlgebraicDisplaySimplifierQuadraticValue second)
    {
        return TryQuadraticRoots(polynomial[2], polynomial[1], polynomial[0], out first, out second);
    }

    private static bool TryQuadraticRoots(BigRational a, BigRational b, BigRational c, out AlgebraicDisplaySimplifierQuadraticValue first, out AlgebraicDisplaySimplifierQuadraticValue second)
    {
        BigRational discriminant = b * b - new BigRational(4) * a * c;
        if (a.IsZero || discriminant.Sign < 0 || !TrySquareRoot(discriminant, out AlgebraicDisplaySimplifierQuadraticValue squareRoot))
        {
            first = default;
            second = default;
            return false;
        }

        BigRational center = -b / (new BigRational(2) * a);
        BigRational scale = BigRational.One / (new BigRational(2) * a.Abs());
        AlgebraicDisplaySimplifierQuadraticValue magnitude = squareRoot.Multiply(scale);
        first = AlgebraicDisplaySimplifierQuadraticValue.Rational(center).Subtract(magnitude);
        second = AlgebraicDisplaySimplifierQuadraticValue.Rational(center).Add(magnitude);
        return true;
    }

    private static bool TrySquareRoot(BigRational value, out AlgebraicDisplaySimplifierQuadraticValue result)
    {
        if (value.Sign < 0)
        {
            result = default;
            return false;
        }

        if (BigRational.TrySquareRoot(value, out BigRational rational))
        {
            result = AlgebraicDisplaySimplifierQuadraticValue.Rational(rational);
            return true;
        }

        ExactInteger radicand = value.Numerator * value.Denominator;
        (ExactInteger outside, ExactInteger inside) = ExtractSquareFactor(radicand);
        result = AlgebraicDisplaySimplifierQuadraticValue.Create(BigRational.Zero, new BigRational(outside, value.Denominator), inside);
        return true;
    }

    private static (ExactInteger Outside, ExactInteger Inside) ExtractSquareFactor(ExactInteger value)
    {
        value = ExactInteger.Abs(value);
        if (value.IsZero)
        {
            return (ExactInteger.Zero, ExactInteger.One);
        }

        if (TryIntegerSquareRoot(value, out ExactInteger exact))
        {
            return (exact, ExactInteger.One);
        }

        ExactInteger outside = ExactInteger.One;
        ExactInteger remaining = value;
        for (int factor = 2; factor <= MaxSquareFactorTrial && new ExactInteger(factor) * factor <= remaining; factor++)
        {
            ExactInteger square = new ExactInteger(factor) * factor;
            while (remaining % square == 0)
            {
                outside *= factor;
                remaining /= square;
            }
        }

        if (TryIntegerSquareRoot(remaining, out ExactInteger remainingRoot))
        {
            outside *= remainingRoot;
            remaining = ExactInteger.One;
        }

        return (outside, remaining);
    }

    private static (ExactInteger Outside, ExactInteger Inside) ExtractCubeFactor(ExactInteger value)
    {
        value = ExactInteger.Abs(value);
        if (value.IsZero)
        {
            return (ExactInteger.Zero, ExactInteger.One);
        }

        if (TryIntegerCubeRoot(value, out ExactInteger exact))
        {
            return (exact, ExactInteger.One);
        }

        ExactInteger outside = ExactInteger.One;
        ExactInteger remaining = value;
        for (int factor = 2; factor <= MaxCubeFactorTrial && new ExactInteger(factor) * factor * factor <= remaining; factor++)
        {
            ExactInteger cube = new ExactInteger(factor) * factor * factor;
            while (remaining % cube == 0)
            {
                outside *= factor;
                remaining /= cube;
            }
        }

        if (TryIntegerCubeRoot(remaining, out ExactInteger remainingRoot))
        {
            outside *= remainingRoot;
            remaining = ExactInteger.One;
        }

        return (outside, remaining);
    }

    private static bool TryIntegerSquareRoot(ExactInteger value, out ExactInteger root)
    {
        if (value.Sign < 0)
        {
            root = default;
            return false;
        }

        if (value < 2)
        {
            root = value;
            return true;
        }

        ExactInteger candidate = ExactInteger.One << checked((int)((value.GetBitLength() + 1) / 2));
        while (true)
        {
            ExactInteger next = (candidate + value / candidate) >> 1;
            if (next >= candidate)
            {
                root = candidate;
                return candidate * candidate == value;
            }

            candidate = next;
        }
    }

    private static bool TryIntegerCubeRoot(ExactInteger value, out ExactInteger root)
    {
        if (value.Sign < 0)
        {
            root = default;
            return false;
        }

        if (value < 2)
        {
            root = value;
            return true;
        }

        int highBit = checked((int)((value.GetBitLength() + 2) / 3));
        ExactInteger low = ExactInteger.Zero;
        ExactInteger high = ExactInteger.One << highBit;
        while (high - low > ExactInteger.One)
        {
            ExactInteger middle = (low + high) >> 1;
            ExactInteger cube = middle * middle * middle;
            if (cube <= value)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        root = low;
        return low * low * low == value;
    }

    private static bool IsInside(AlgebraicDisplaySimplifierQuadraticValue value, RationalInterval interval)
    {
        return value.Subtract(AlgebraicDisplaySimplifierQuadraticValue.Rational(interval.Lower)).Sign > 0 &&
               value.Subtract(AlgebraicDisplaySimplifierQuadraticValue.Rational(interval.Upper)).Sign < 0;
    }

    private static bool IsSignedSquareRootInside(AlgebraicDisplaySimplifierQuadraticValue square, int sign, RationalInterval interval)
    {
        BigRational lower = sign > 0 ? interval.Lower : -interval.Upper;
        BigRational upper = sign > 0 ? interval.Upper : -interval.Lower;
        if (upper.Sign <= 0)
        {
            return false;
        }

        bool aboveLower = lower.Sign < 0 || square.Subtract(AlgebraicDisplaySimplifierQuadraticValue.Rational(lower * lower)).Sign > 0;
        bool belowUpper = square.Subtract(AlgebraicDisplaySimplifierQuadraticValue.Rational(upper * upper)).Sign < 0;
        return aboveLower && belowUpper;
    }

    private static AlgebraicDisplaySimplifierQuadraticValue Evaluate(UnivariatePolynomial polynomial, AlgebraicDisplaySimplifierQuadraticValue argument)
    {
        AlgebraicDisplaySimplifierQuadraticValue result = AlgebraicDisplaySimplifierQuadraticValue.Rational(BigRational.Zero);
        for (int degree = polynomial.Degree; degree >= 0; degree--)
        {
            result = result.Multiply(argument).Add(AlgebraicDisplaySimplifierQuadraticValue.Rational(polynomial[degree]));
        }

        return result;
    }

    private static AlgebraicDisplaySimplifierExtensionValue EvaluateExtension(UnivariatePolynomial polynomial, AlgebraicDisplaySimplifierQuadraticValue square)
    {
        AlgebraicDisplaySimplifierExtensionValue argument = AlgebraicDisplaySimplifierExtensionValue.Argument();
        AlgebraicDisplaySimplifierExtensionValue result = AlgebraicDisplaySimplifierExtensionValue.Rational(BigRational.Zero);
        for (int degree = polynomial.Degree; degree >= 0; degree--)
        {
            result = result.Multiply(argument, square).Add(AlgebraicDisplaySimplifierExtensionValue.Rational(polynomial[degree]));
        }

        return result;
    }

    private static string FormatRoot(AlgebraicDisplaySimplifierRootForm form)
    {
        return form.IsDirect ? FormatQuadratic(form.DirectValue) : FormatSignedSquareRoot(form.Square, form.RootSign);
    }

    private static string FormatExtension(AlgebraicDisplaySimplifierExtensionValue value, AlgebraicDisplaySimplifierQuadraticValue square, int rootSign)
    {
        if (value.Odd.IsZero)
        {
            return FormatQuadratic(value.Even);
        }

        if (value.Even.IsZero)
        {
            AlgebraicDisplaySimplifierQuadraticValue squared = value.Odd.Multiply(value.Odd).Multiply(square);
            int sign = value.Odd.Sign * rootSign;
            return FormatSignedSquareRoot(squared, sign);
        }

        string even = FormatQuadratic(value.Even);
        int termSign = value.Odd.Sign * rootSign;
        AlgebraicDisplaySimplifierQuadraticValue oddSquare = value.Odd.Multiply(value.Odd).Multiply(square);
        string odd = FormatSignedSquareRoot(oddSquare, 1);
        return termSign < 0 ? $"{even} − {odd}" : $"{even} + {odd}";
    }

    private static string FormatSignedSquareRoot(AlgebraicDisplaySimplifierQuadraticValue value, int sign)
    {
        if (TryPositiveSquareRootInField(value, out AlgebraicDisplaySimplifierQuadraticValue fieldRoot))
        {
            return FormatQuadratic(sign < 0 ? fieldRoot.Negate() : fieldRoot);
        }

        string magnitude = FormatNestedSquareRoot(value);
        return sign < 0 ? "−" + magnitude : magnitude;
    }

    private static bool TryPositiveSquareRootInField(AlgebraicDisplaySimplifierQuadraticValue value, out AlgebraicDisplaySimplifierQuadraticValue result)
    {
        if (value.Sign < 0)
        {
            result = default;
            return false;
        }

        if (value.IsRational)
        {
            return TrySquareRoot(value.RationalPart, out result);
        }

        BigRational norm = value.RationalPart * value.RationalPart - value.RadicalCoefficient * value.RadicalCoefficient * new BigRational(value.Radicand);
        if (norm.Sign < 0 || !BigRational.TrySquareRoot(norm, out BigRational normRoot))
        {
            result = default;
            return false;
        }

        foreach (BigRational signedNorm in new[]
        {
            normRoot,
            -normRoot
        }

        )
        {
            BigRational rationalSquare = (value.RationalPart + signedNorm) / 2;
            if (!BigRational.TrySquareRoot(rationalSquare, out BigRational rationalPart) || rationalPart.IsZero)
            {
                continue;
            }

            BigRational radicalPart = value.RadicalCoefficient / (new BigRational(2) * rationalPart);
            AlgebraicDisplaySimplifierQuadraticValue candidate = AlgebraicDisplaySimplifierQuadraticValue.Create(rationalPart, radicalPart, value.Radicand);
            if (candidate.Multiply(candidate) != value)
            {
                continue;
            }

            result = candidate.Sign < 0 ? candidate.Negate() : candidate;
            return true;
        }

        result = default;
        return false;
    }

    private static string FormatNestedSquareRoot(AlgebraicDisplaySimplifierQuadraticValue value)
    {
        if (value.IsRational)
        {
            _ = TrySquareRoot(value.RationalPart, out AlgebraicDisplaySimplifierQuadraticValue squareRoot);
            return FormatQuadratic(squareRoot);
        }

        ExactInteger commonDenominator = LeastCommonMultiple(value.RationalPart.Denominator, value.RadicalCoefficient.Denominator);
        ExactInteger rationalInteger = value.RationalPart.Numerator * (commonDenominator / value.RationalPart.Denominator);
        ExactInteger radicalInteger = value.RadicalCoefficient.Numerator * (commonDenominator / value.RadicalCoefficient.Denominator);
        ExactInteger innerRational = rationalInteger * commonDenominator;
        ExactInteger innerRadical = radicalInteger * commonDenominator;
        ExactInteger common = ExactInteger.GreatestCommonDivisor(ExactInteger.Abs(innerRational), ExactInteger.Abs(innerRadical));
        (ExactInteger outside, _) = ExtractSquareFactor(common);
        ExactInteger outsideSquare = outside * outside;
        innerRational /= outsideSquare;
        innerRadical /= outsideSquare;
        BigRational coefficient = new(outside, commonDenominator);
        string inner = FormatIntegerQuadratic(innerRational, innerRadical, value.Radicand);
        return FormatScaledRadical(coefficient, inner);
    }

    private static ExactInteger LeastCommonMultiple(ExactInteger left, ExactInteger right)
    {
        return left / ExactInteger.GreatestCommonDivisor(left, right) * right;
    }

    private static string FormatIntegerQuadratic(ExactInteger rational, ExactInteger radicalCoefficient, ExactInteger radicand)
    {
        string radical = FormatIntegerRadicalMagnitude(ExactInteger.Abs(radicalCoefficient), radicand);
        if (rational.IsZero)
        {
            return radicalCoefficient.Sign < 0 ? "−" + radical : radical;
        }

        string rationalText = Integer(rational);
        return radicalCoefficient.Sign < 0 ? $"{rationalText} − {radical}" : $"{rationalText} + {radical}";
    }

    private static string FormatQuadratic(AlgebraicDisplaySimplifierQuadraticValue value)
    {
        if (value.IsRational)
        {
            return Rational(value.RationalPart);
        }

        string radical = FormatRadicalMagnitude(value.RadicalCoefficient.Abs(), value.Radicand);
        if (value.RationalPart.IsZero)
        {
            return value.RadicalCoefficient.Sign < 0 ? "−" + radical : radical;
        }

        if (value.RadicalCoefficient.Sign < 0)
        {
            return value.RationalPart.Sign < 0 ? $"−{radical} − {Rational(value.RationalPart.Abs())}" : $"{Rational(value.RationalPart)} − {radical}";
        }

        return value.RationalPart.Sign < 0 ? $"{radical} − {Rational(value.RationalPart.Abs())}" : $"{radical} + {Rational(value.RationalPart)}";
    }

    private static string FormatRadicalMagnitude(BigRational coefficient, ExactInteger radicand)
    {
        return FormatScaledRadical(coefficient, radicand.ToString(CultureInfo.InvariantCulture));
    }

    private static string FormatIntegerRadicalMagnitude(ExactInteger coefficient, ExactInteger radicand)
    {
        string root = $"sqrt({radicand.ToString(CultureInfo.InvariantCulture)})";
        return coefficient.IsOne ? root : coefficient.ToString(CultureInfo.InvariantCulture) + root;
    }

    private static string FormatScaledRadical(BigRational coefficient, string radicand)
    {
        string root = $"sqrt({radicand})";
        string numerator = coefficient.Numerator.IsOne ? root : coefficient.Numerator.ToString(CultureInfo.InvariantCulture) + root;
        return coefficient.Denominator.IsOne ? numerator : numerator + "/" + coefficient.Denominator.ToString(CultureInfo.InvariantCulture);
    }

    private static string Rational(BigRational value)
    {
        return value.ToString().Replace("-", "−", StringComparison.Ordinal);
    }

    private static string Integer(ExactInteger value)
    {
        return value.ToString(CultureInfo.InvariantCulture).Replace("-", "−", StringComparison.Ordinal);
    }
}
