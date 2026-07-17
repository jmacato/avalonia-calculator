namespace Graphing.Symbolics;

/// <summary>
/// Constructs principal inverse-trigonometric angles in the request's unit.
/// The underlying symbolic functions are radian-valued; known rational
/// special values are reduced directly to exact fractions of a turn.
/// </summary>
internal static class ExactInverseTrigonometry
{
    public static ExactReal PrincipalAngle(
        string inverse,
        ExactScalar target,
        AngleUnit angleUnit,
        bool normalizeOddNegative = true,
        bool preserveRadianZeroAsPiFraction = false)
    {
        if (target.RationalValue is { } rational &&
            TrySpecialFraction(inverse, rational, out BigRational piFraction))
        {
            return piFraction.IsZero &&
                   !(preserveRadianZeroAsPiFraction && angleUnit == AngleUnit.Radians)
                ? new RationalReal(BigRational.Zero)
                : ExactAngleArithmetic.PiFraction(angleUnit, piFraction);
        }

        ExactReal radians = normalizeOddNegative &&
                           target.Sign < 0 &&
                           inverse is "asin" or "atan"
            ? ExactRealArithmetic.Negate(
                new FunctionReal(inverse, [target.Negate().Value]))
            : new FunctionReal(inverse, [target.Value]);
        return ExactAngleArithmetic.FromRadians(radians, angleUnit);
    }

    private static bool TrySpecialFraction(
        string inverse,
        BigRational target,
        out BigRational piFraction)
    {
        BigRational? result = inverse switch
        {
            "asin" => target switch
            {
                var value when value == BigRational.MinusOne => new BigRational(-1, 2),
                var value when value == new BigRational(-1, 2) => new BigRational(-1, 6),
                var value when value.IsZero => BigRational.Zero,
                var value when value == new BigRational(1, 2) => new BigRational(1, 6),
                var value when value.IsOne => new BigRational(1, 2),
                _ => null
            },
            "acos" => target switch
            {
                var value when value == BigRational.MinusOne => BigRational.One,
                var value when value == new BigRational(-1, 2) => new BigRational(2, 3),
                var value when value.IsZero => new BigRational(1, 2),
                var value when value == new BigRational(1, 2) => new BigRational(1, 3),
                var value when value.IsOne => BigRational.Zero,
                _ => null
            },
            "atan" => target switch
            {
                var value when value == BigRational.MinusOne => new BigRational(-1, 4),
                var value when value.IsZero => BigRational.Zero,
                var value when value.IsOne => new BigRational(1, 4),
                _ => null
            },
            _ => throw new ArgumentOutOfRangeException(nameof(inverse))
        };
        if (result is not { } exact)
        {
            piFraction = default;
            return false;
        }

        piFraction = exact;
        return true;
    }
}
