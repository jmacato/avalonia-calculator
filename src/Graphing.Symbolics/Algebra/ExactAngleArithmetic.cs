namespace Graphing.Symbolics;

/// <summary>
/// Converts exact angles at the boundary between request-unit coordinates and
/// <see cref="FunctionReal"/> arguments. FunctionReal trigonometric functions
/// always use radians; solver coordinates use the request's angle unit.
/// </summary>
internal static class ExactAngleArithmetic
{
    public static ExactReal PiFraction(AngleUnit angleUnit, BigRational piFraction)
    {
        return angleUnit switch
        {
            AngleUnit.Radians => new AffinePiReal(piFraction, BigRational.Zero),
            AngleUnit.Degrees => new RationalReal(new BigRational(180) * piFraction),
            AngleUnit.Grads => new RationalReal(new BigRational(200) * piFraction),
            _ => throw new ArgumentOutOfRangeException(nameof(angleUnit))
        };
    }

    public static ExactReal FromRadians(ExactReal radians, AngleUnit angleUnit)
    {
        if (angleUnit == AngleUnit.Radians)
        {
            return radians;
        }

        return ExactRealArithmetic.Divide(
            ExactRealArithmetic.Scale(radians, HalfTurn(angleUnit)),
            new AffinePiReal(BigRational.One, BigRational.Zero));
    }

    public static ExactReal ToRadians(ExactReal angle, AngleUnit angleUnit)
    {
        if (angleUnit == AngleUnit.Radians)
        {
            return angle;
        }

        return ExactRealArithmetic.Multiply(
            angle,
            new AffinePiReal(HalfTurn(angleUnit).Reciprocal(), BigRational.Zero));
    }

    public static BigRational HalfTurn(AngleUnit angleUnit)
    {
        return angleUnit switch
        {
            AngleUnit.Degrees => new BigRational(180),
            AngleUnit.Grads => new BigRational(200),
            _ => throw new ArgumentOutOfRangeException(nameof(angleUnit))
        };
    }
}
