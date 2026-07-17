using System.Collections.Immutable;
using System.Globalization;
using Graphing.Symbolics;

namespace GraphingImpl;
/// <summary>
/// Reproduces the representative choices used by the captured Windows
/// affine-tangent matrix. This never changes a certified set; it only chooses
/// an equivalent member of each periodic family for the public string.
/// </summary>
internal static class CapturedAffineTangentCompatibilityFormatter
{
    private const string Parameter = "n₁";
    public static bool TryDomain(RealSet set, string variable, out string value)
    {
        if (set is not PeriodicIntervalSet { Constraint.Bound.IsUnbounded: true, Intervals: [{ IncludesLower: false, IncludesUpper: false } interval] } periodic)
        {
            value = string.Empty;
            return false;
        }

        value = $"{variable} ≠ {PeriodicExpression(interval.UpperOffset, periodic.Period, CapturedAffineTangentCompatibilityFormatterDisplayStyle.Domain)}, " + $"∀ {Parameter} ∈ ℤ";
        return true;
    }

    public static bool TryZeros(RealSet set, string variable, out string value)
    {
        if (set is not PeriodicPointSet { Constraint.Bound.IsUnbounded: true } periodic)
        {
            value = string.Empty;
            return false;
        }

        value = $"{variable} = {PeriodicExpression(periodic.Offset, periodic.Period, CapturedAffineTangentCompatibilityFormatterDisplayStyle.Zero)}, " + $"{Parameter} ∈ ℤ";
        return true;
    }

    public static bool TryVerticalAsymptotes(ImmutableArray<Asymptote> asymptotes, out ImmutableArray<string> values)
    {
        if (asymptotes is not [{ Orientation: AsymptoteOrientation.Vertical, Coordinate: PeriodicReal { Constraint.Bound.IsUnbounded: true } periodic }])
        {
            values = [];
            return false;
        }

        values = [$"x = {PeriodicExpression(periodic.Offset, periodic.Period, CapturedAffineTangentCompatibilityFormatterDisplayStyle.Vertical)}, " + $"{Parameter} ∈ ℤ"];
        return true;
    }

    public static bool TryMonotoneRegion(MonotoneRegion region, out string value)
    {
        if (region.Region is not PeriodicIntervalSet { Constraint.Bound.IsUnbounded: true, Intervals: [{ IncludesLower: false, IncludesUpper: false } interval] } periodic)
        {
            value = string.Empty;
            return false;
        }

        (ExactReal lower, ExactReal upper) = MonotoneInterval(interval.LowerOffset, interval.UpperOffset, periodic.Period);
        string multiplier = PeriodMultiplier(periodic.Period);
        value = $"({AddOffset(multiplier, Offset(lower, CapturedAffineTangentCompatibilityFormatterDisplayStyle.Monotonicity))}, " + $"{AddOffset(multiplier, Offset(upper, CapturedAffineTangentCompatibilityFormatterDisplayStyle.Monotonicity))}), " + $"{Parameter} ∈ ℤ";
        return true;
    }

    private static string PeriodicExpression(ExactReal offset, ExactReal period, CapturedAffineTangentCompatibilityFormatterDisplayStyle style)
    {
        period = ExactAngleDisplayNormalizer.Normalize(period);
        offset = ExactAngleDisplayNormalizer.Normalize(offset);
        if (style == CapturedAffineTangentCompatibilityFormatterDisplayStyle.Zero)
        {
            offset = PositiveZeroOffset(offset, period);
        }
        else if (style == CapturedAffineTangentCompatibilityFormatterDisplayStyle.Domain)
        {
            offset = CanonicalDomainOffset(offset, period);
        }

        return AddOffset(PeriodMultiplier(period), Offset(offset, style));
    }

    private static ExactReal CanonicalDomainOffset(ExactReal offset, ExactReal period)
    {
        // Windows chooses the zero representative when an affine-pi or
        // rational pole offset is an integral number of periods. Quotient
        // offsets such as 1/2 - 1/pi retain their captured expanded form.
        if (TryPiRatio(offset, out _, out _))
        {
            return offset;
        }

        if (offset is RationalReal rationalOffset && period is RationalReal { Value.Sign: > 0 } rationalPeriod)
        {
            return new RationalReal(Mod(rationalOffset.Value, rationalPeriod.Value));
        }

        if (offset is AffinePiReal { Constant.IsZero: true } affineOffset && period is AffinePiReal { PiCoefficient.Sign: > 0, Constant.IsZero: true } affinePeriod)
        {
            return affineOffset with
            {
                PiCoefficient = Mod(affineOffset.PiCoefficient, affinePeriod.PiCoefficient)
            };
        }

        return offset;
    }

    private static ExactReal PositiveZeroOffset(ExactReal offset, ExactReal period)
    {
        if (TryPiRatio(offset, out _, out _))
        {
            // Keep the quotient structural. Offset() combines a negative
            // reciprocal-pi residue with the rational period exactly.
            return offset;
        }

        if (offset is RationalReal rationalOffset && period is RationalReal { Value.Sign: > 0 } rationalPeriod)
        {
            return new RationalReal(Mod(rationalOffset.Value, rationalPeriod.Value));
        }

        if (offset is AffinePiReal { Constant.IsZero: true } affineOffset && period is AffinePiReal { PiCoefficient.Sign: > 0, Constant.IsZero: true } affinePeriod)
        {
            return affineOffset with
            {
                PiCoefficient = Mod(affineOffset.PiCoefficient, affinePeriod.PiCoefficient)
            };
        }

        if (DefinitelyNegative(offset))
        {
            return ExactAngleDisplayNormalizer.Normalize(ExactRealArithmetic.Add(offset, period));
        }

        return offset;
    }

    private static (ExactReal Lower, ExactReal Upper) MonotoneInterval(ExactReal lower, ExactReal upper, ExactReal period)
    {
        lower = ExactAngleDisplayNormalizer.Normalize(lower);
        upper = ExactAngleDisplayNormalizer.Normalize(upper);
        period = ExactAngleDisplayNormalizer.Normalize(period);
        if (TryPiRatio(lower, out _, out _) || TryPiRatio(upper, out _, out _))
        {
            return (lower, upper);
        }

        ExactReal canonicalLower = lower;
        if (lower is AffinePiReal { Constant.IsZero: true } affineLower && period is AffinePiReal { PiCoefficient.Sign: > 0, Constant.IsZero: true } affinePeriod)
        {
            canonicalLower = affineLower with
            {
                PiCoefficient = Mod(affineLower.PiCoefficient, affinePeriod.PiCoefficient)
            };
        }
        else if (lower is RationalReal rationalLower && period is RationalReal { Value.Sign: > 0 } rationalPeriod)
        {
            canonicalLower = new RationalReal(Mod(rationalLower.Value, rationalPeriod.Value));
        }

        if (string.Equals(ExactRealCanonical.Format(canonicalLower), ExactRealCanonical.Format(lower), StringComparison.Ordinal))
        {
            return (lower, upper);
        }

        ExactReal width = ExactAngleDisplayNormalizer.Normalize(ExactRealArithmetic.Subtract(upper, lower));
        ExactReal canonicalUpper = ExactAngleDisplayNormalizer.Normalize(ExactRealArithmetic.Add(canonicalLower, width));
        return (canonicalLower, canonicalUpper);
    }

    private static string Offset(ExactReal value, CapturedAffineTangentCompatibilityFormatterDisplayStyle style)
    {
        value = ExactAngleDisplayNormalizer.Normalize(value);
        if (TryPiRatio(value, out BigRational rationalPart, out BigRational reciprocalPi))
        {
            if (style == CapturedAffineTangentCompatibilityFormatterDisplayStyle.Zero && rationalPart.IsZero && reciprocalPi.Sign < 0)
            {
                rationalPart = BigRational.One;
            }

            return style == CapturedAffineTangentCompatibilityFormatterDisplayStyle.Zero ? CommonPiRatio(rationalPart, reciprocalPi) : ExpandedPiRatio(rationalPart, reciprocalPi);
        }

        if (style is CapturedAffineTangentCompatibilityFormatterDisplayStyle.Zero or CapturedAffineTangentCompatibilityFormatterDisplayStyle.Vertical && value is AffinePiReal { PiCoefficient.IsZero: false, Constant.IsZero: false } affine)
        {
            return CommonAffinePi(affine);
        }

        return SymbolicsCompatibilityFormatter.Real(value);
    }

    private static bool TryPiRatio(ExactReal value, out BigRational rationalPart, out BigRational reciprocalPi)
    {
        if (value is FunctionReal { Function: "divide", Arguments: [AffinePiReal numerator, AffinePiReal { PiCoefficient.IsZero: false, Constant.IsZero: true } denominator] })
        {
            rationalPart = numerator.PiCoefficient / denominator.PiCoefficient;
            reciprocalPi = numerator.Constant / denominator.PiCoefficient;
            return true;
        }

        rationalPart = default;
        reciprocalPi = default;
        return false;
    }

    private static string ExpandedPiRatio(BigRational rationalPart, BigRational reciprocalPi)
    {
        string result = string.Empty;
        if (!reciprocalPi.IsZero)
        {
            string magnitude = RationalOverPi(reciprocalPi.Abs());
            result = reciprocalPi.Sign < 0 ? "−" + magnitude : magnitude;
        }

        if (!rationalPart.IsZero)
        {
            string rational = Rational(rationalPart.Abs());
            if (result.Length == 0)
            {
                result = rationalPart.Sign < 0 ? "−" + rational : rational;
            }
            else
            {
                result += rationalPart.Sign < 0 ? " − " + rational : " + " + rational;
            }
        }

        return result.Length == 0 ? "0" : result;
    }

    private static string CommonPiRatio(BigRational rationalPart, BigRational reciprocalPi)
    {
        if (rationalPart.IsZero)
        {
            string quotient = RationalOverPi(reciprocalPi.Abs());
            return reciprocalPi.Sign < 0 ? "−" + quotient : quotient;
        }

        ExactInteger denominator = Lcm(rationalPart.Denominator, reciprocalPi.Denominator);
        ExactInteger piNumerator = rationalPart.Numerator * (denominator / rationalPart.Denominator);
        ExactInteger constantNumerator = reciprocalPi.Numerator * (denominator / reciprocalPi.Denominator);
        string numerator = LinearPiNumerator(piNumerator, constantNumerator);
        string divisor = denominator.IsOne ? "π" : $"{denominator.ToString(CultureInfo.InvariantCulture)}π";
        return $"({numerator})/{divisor}";
    }

    private static string CommonAffinePi(AffinePiReal value)
    {
        ExactInteger denominator = Lcm(value.PiCoefficient.Denominator, value.Constant.Denominator);
        ExactInteger piNumerator = value.PiCoefficient.Numerator * (denominator / value.PiCoefficient.Denominator);
        ExactInteger constantNumerator = value.Constant.Numerator * (denominator / value.Constant.Denominator);
        ExactInteger commonFactor = ExactInteger.GreatestCommonDivisor(ExactInteger.Abs(piNumerator), ExactInteger.Abs(constantNumerator));
        if (denominator.IsOne && commonFactor > ExactInteger.One)
        {
            return $"{commonFactor.ToString(CultureInfo.InvariantCulture)}(" + $"{LinearPiNumerator(piNumerator / commonFactor, constantNumerator / commonFactor)})";
        }

        string numerator = LinearPiNumerator(piNumerator, constantNumerator);
        return denominator.IsOne ? numerator : $"({numerator})/{denominator.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string LinearPiNumerator(ExactInteger pi, ExactInteger constant)
    {
        var parts = new List<string>(2);
        if (!pi.IsZero)
        {
            ExactInteger absolute = ExactInteger.Abs(pi);
            string coefficient = absolute.IsOne ? string.Empty : absolute.ToString(CultureInfo.InvariantCulture);
            parts.Add((pi.Sign < 0 ? "−" : string.Empty) + coefficient + "π");
        }

        if (!constant.IsZero)
        {
            string magnitude = ExactInteger.Abs(constant).ToString(CultureInfo.InvariantCulture);
            if (parts.Count == 0)
            {
                parts.Add((constant.Sign < 0 ? "−" : string.Empty) + magnitude);
            }
            else
            {
                parts.Add((constant.Sign < 0 ? "− " : "+ ") + magnitude);
            }
        }

        return string.Join(" ", parts);
    }

    private static string RationalOverPi(BigRational value)
    {
        if (value.IsOne)
        {
            return "1/π";
        }

        if (value.Denominator.IsOne)
        {
            return $"{value.Numerator.ToString(CultureInfo.InvariantCulture)}/π";
        }

        return $"{value.Numerator.ToString(CultureInfo.InvariantCulture)}/" + $"({value.Denominator.ToString(CultureInfo.InvariantCulture)}π)";
    }

    private static string PeriodMultiplier(ExactReal period)
    {
        period = ExactAngleDisplayNormalizer.Normalize(period);
        if (period is RationalReal rational)
        {
            if (rational.Value.IsOne)
            {
                return Parameter;
            }

            return $"{Rational(rational.Value)}{Parameter}";
        }

        if (period is AffinePiReal { PiCoefficient.Sign: > 0, Constant.IsZero: true } affine)
        {
            BigRational coefficient = affine.PiCoefficient;
            string numerator = coefficient.Numerator.IsOne ? "π" : $"{coefficient.Numerator.ToString(CultureInfo.InvariantCulture)}π";
            return coefficient.Denominator.IsOne ? $"{numerator}{Parameter}" : $"{numerator}/{coefficient.Denominator.ToString(CultureInfo.InvariantCulture)}{Parameter}";
        }

        return $"{SymbolicsCompatibilityFormatter.Real(period)}{Parameter}";
    }

    private static string AddOffset(string multiplier, string offset)
    {
        if (offset == "0")
        {
            return multiplier;
        }

        return offset.StartsWith('−') ? $"{multiplier} − {offset[1..]}" : $"{multiplier} + {offset}";
    }

    private static bool DefinitelyNegative(ExactReal value) => value switch
    {
        RationalReal rational => rational.Value.Sign < 0,
        AffinePiReal affine => affine.PiCoefficient.Sign <= 0 && affine.Constant.Sign <= 0 && (!affine.PiCoefficient.IsZero || !affine.Constant.IsZero),
        FunctionReal { Function: "divide", Arguments: [var numerator, var denominator] } => DefinitelyNegative(numerator) && DefinitelyPositive(denominator),
        FunctionReal { Function: "negate", Arguments: [var operand] } => DefinitelyPositive(operand),
        _ => false
    };
    private static bool DefinitelyPositive(ExactReal value) => value switch
    {
        RationalReal rational => rational.Value.Sign > 0,
        AffinePiReal affine => affine.PiCoefficient.Sign >= 0 && affine.Constant.Sign >= 0 && (!affine.PiCoefficient.IsZero || !affine.Constant.IsZero),
        NamedReal { Name: "e" or "pi" } => true,
        FunctionReal { Function: "negate", Arguments: [var operand] } => DefinitelyNegative(operand),
        _ => false
    };
    private static BigRational Mod(BigRational value, BigRational modulus)
    {
        ExactInteger quotient = (value / modulus).Floor();
        return value - new BigRational(quotient) * modulus;
    }

    private static ExactInteger Lcm(ExactInteger left, ExactInteger right) => ExactInteger.Abs(left / ExactInteger.GreatestCommonDivisor(left, right) * right);
    private static string Rational(BigRational value) => value.ToString().Replace("-", "−", StringComparison.Ordinal);
}
