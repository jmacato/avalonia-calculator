using System.Collections.Immutable;
using Graphing.Symbolics;

namespace GraphingImpl;

/// <summary>
/// Reduces exact angle expressions produced by the proof engine to the
/// principal-angle forms used by Calculator. This is presentation-only:
/// every reduction below is an exact identity over <see cref="ExactReal"/>.
/// </summary>
internal static class ExactAngleDisplayNormalizer
{
    private const int MaximumDepth = 64;

    public static ExactReal Normalize(ExactReal value)
    {
        return Normalize(value, 0);
    }

    private static ExactReal Normalize(ExactReal value, int depth)
    {
        if (depth >= MaximumDepth || value is not FunctionReal function)
        {
            return value;
        }

        ImmutableArray<ExactReal> arguments = function.Arguments
            .Select(argument => Normalize(argument, depth + 1))
            .ToImmutableArray();

        if (TrySpecialInverseAngle(function.Function, arguments, out ExactReal special) ||
            TrySpecialTwiceArctangent(function.Function, arguments, out special))
        {
            return special;
        }

        ExactReal reduced = function.Function switch
        {
            "negate" when arguments is [var operand] =>
                ExactRealArithmetic.Negate(operand),
            "pi-minus" when arguments is [var operand] =>
                ExactRealArithmetic.Subtract(Pi, operand),
            "scale" when arguments is [var operand, RationalReal factor] =>
                ExactRealArithmetic.Scale(operand, factor.Value),
            "add" when arguments is [var left, var right] =>
                ExactRealArithmetic.Add(left, right),
            "multiply" when arguments is [var left, var right] =>
                ExactRealArithmetic.Multiply(left, right),
            "divide" when arguments is [var numerator, var denominator] =>
                ExactRealArithmetic.Divide(numerator, denominator),
            "affine" when arguments is
                [var operand, RationalReal scale, RationalReal shift] =>
                ExactRealArithmetic.AddRational(
                    ExactRealArithmetic.Scale(operand, scale.Value),
                    shift.Value),
            _ => function with { Arguments = arguments }
        };

        // Preserve the solver's structural spelling unless the exact
        // arithmetic actually closed into the rational-plus-pi field. In
        // particular, rewriting sqrt(3)/2 as 1/2(sqrt(3)) is exact but is a
        // presentation regression.
        return reduced is RationalReal or AffinePiReal
            ? reduced
            : function with { Arguments = arguments };
    }

    private static AffinePiReal Pi { get; } =
        new(BigRational.One, BigRational.Zero);

    private static bool TrySpecialInverseAngle(
        string function,
        ImmutableArray<ExactReal> arguments,
        out ExactReal value)
    {
        if (arguments is not [RationalReal rational])
        {
            value = null!;
            return false;
        }

        BigRational argument = rational.Value;
        BigRational? piCoefficient = function switch
        {
            "asin" or "arcsin" => argument switch
            {
                var x when x == BigRational.MinusOne => new BigRational(-1, 2),
                var x when x == new BigRational(-1, 2) => new BigRational(-1, 6),
                var x when x.IsZero => BigRational.Zero,
                var x when x == new BigRational(1, 2) => new BigRational(1, 6),
                var x when x.IsOne => new BigRational(1, 2),
                _ => null
            },
            "acos" or "arccos" => argument switch
            {
                var x when x == BigRational.MinusOne => BigRational.One,
                var x when x == new BigRational(-1, 2) => new BigRational(2, 3),
                var x when x.IsZero => new BigRational(1, 2),
                var x when x == new BigRational(1, 2) => new BigRational(1, 3),
                var x when x.IsOne => BigRational.Zero,
                _ => null
            },
            "atan" or "arctan" => argument switch
            {
                var x when x == BigRational.MinusOne => new BigRational(-1, 4),
                var x when x.IsZero => BigRational.Zero,
                var x when x.IsOne => new BigRational(1, 4),
                _ => null
            },
            _ => null
        };

        if (piCoefficient is not { } coefficient)
        {
            value = null!;
            return false;
        }

        value = coefficient.IsZero
            ? new RationalReal(BigRational.Zero)
            : new AffinePiReal(coefficient, BigRational.Zero);
        return true;
    }

    private static bool TrySpecialTwiceArctangent(
        string function,
        ImmutableArray<ExactReal> arguments,
        out ExactReal value)
    {
        if (function != "twice-atan" ||
            arguments is not [var argument] ||
            !TryLinearSqrtTwo(argument, out BigRational constant, out BigRational radical))
        {
            value = null!;
            return false;
        }

        BigRational? coefficient = (constant, radical) switch
        {
            (var a, var b) when a == BigRational.MinusOne && b.IsOne =>
                new BigRational(1, 4),
            (var a, var b) when a.IsOne && b == BigRational.MinusOne =>
                new BigRational(-1, 4),
            (var a, var b) when a.IsOne && b.IsOne =>
                new BigRational(3, 4),
            (var a, var b) when a == BigRational.MinusOne && b == BigRational.MinusOne =>
                new BigRational(-3, 4),
            _ => null
        };
        if (coefficient is not { } exact)
        {
            value = null!;
            return false;
        }

        value = new AffinePiReal(exact, BigRational.Zero);
        return true;
    }

    private static bool TryLinearSqrtTwo(
        ExactReal value,
        out BigRational constant,
        out BigRational radical)
    {
        switch (value)
        {
            case RationalReal rational:
                constant = rational.Value;
                radical = BigRational.Zero;
                return true;
            case FunctionReal
            {
                Function: "sqrt",
                Arguments: [RationalReal { Value: var radicand }]
            } when radicand == new BigRational(2):
                constant = BigRational.Zero;
                radical = BigRational.One;
                return true;
            case AlgebraicReal algebraic
                when TryRecognizeUnitSqrtTwoTranslate(
                    algebraic,
                    out constant,
                    out radical):
                return true;
            case FunctionReal
            {
                Function: "negate",
                Arguments: [var operand]
            } when TryLinearSqrtTwo(operand, out BigRational negatedConstant, out BigRational negatedRadical):
                constant = -negatedConstant;
                radical = -negatedRadical;
                return true;
            case FunctionReal
            {
                Function: "add",
                Arguments: [var left, var right]
            } when TryLinearSqrtTwo(left, out BigRational leftConstant, out BigRational leftRadical) &&
                   TryLinearSqrtTwo(right, out BigRational rightConstant, out BigRational rightRadical):
                constant = leftConstant + rightConstant;
                radical = leftRadical + rightRadical;
                return true;
            case FunctionReal
            {
                Function: "scale",
                Arguments: [var operand, RationalReal factor]
            } when TryLinearSqrtTwo(operand, out BigRational scaledConstant, out BigRational scaledRadical):
                constant = factor.Value * scaledConstant;
                radical = factor.Value * scaledRadical;
                return true;
            default:
                constant = default;
                radical = default;
                return false;
        }
    }

    private static bool TryRecognizeUnitSqrtTwoTranslate(
        AlgebraicReal value,
        out BigRational constant,
        out BigRational radical)
    {
        UnivariatePolynomial polynomial = value.Polynomial;
        if (polynomial.Degree != 2 || polynomial[2].IsZero)
        {
            constant = default;
            radical = default;
            return false;
        }

        BigRational linear = polynomial[1] / polynomial[2];
        BigRational scalar = polynomial[0] / polynomial[2];
        if (scalar != BigRational.MinusOne ||
            linear != new BigRational(2) && linear != new BigRational(-2))
        {
            constant = default;
            radical = default;
            return false;
        }

        int rootSign = value.IsolatingInterval.Lower.Sign >= 0
            ? 1
            : value.IsolatingInterval.Upper.Sign <= 0
                ? -1
                : 0;
        if (rootSign == 0)
        {
            constant = default;
            radical = default;
            return false;
        }

        constant = linear.Sign > 0
            ? BigRational.MinusOne
            : BigRational.One;
        radical = new BigRational(rootSign);
        return true;
    }
}
