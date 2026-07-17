using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal readonly record struct LinearDriftExactOffset(ExactReal Value, int? Sign, BigRational? RationalValue)
{
    public static LinearDriftExactOffset Zero { get; } = new(new RationalReal(BigRational.Zero), 0, BigRational.Zero);
    public bool IsZero => Sign == 0;
    public string Canonical => $"{ExactRealCanonical.Format(Value)}:sign={Sign?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"}";

    public static LinearDriftExactOffset FromRational(BigRational value) => new(new RationalReal(value), value.Sign, value);
    public static LinearDriftExactOffset FromScalar(ExactScalar scalar, BigRational coefficient, ResourceBudget budget)
    {
        ExactScalar scaled = scalar.Multiply(ExactScalar.FromRational(coefficient), budget);
        return new LinearDriftExactOffset(scaled.Value, scaled.Sign, scaled.RationalValue);
    }

    public bool TryAdd(LinearDriftExactOffset other, ResourceBudget budget, out LinearDriftExactOffset result)
    {
        budget.Charge();
        if (RationalValue is { } leftRational && other.RationalValue is { } rightRational)
        {
            BigRational sum = leftRational + rightRational;
            budget.CheckCoefficient(sum);
            result = FromRational(sum);
            return true;
        }

        if (IsZero)
        {
            result = other;
            return true;
        }

        if (other.IsZero)
        {
            result = this;
            return true;
        }

        if (string.Equals(ExactRealCanonical.Format(Value), ExactRealCanonical.Format(ExactRealArithmetic.Negate(other.Value)), StringComparison.Ordinal))
        {
            result = Zero;
            return true;
        }

        ExactReal sumValue = ExactRealArithmetic.Add(Value, other.Value);
        if (TryClassifyStructured(sumValue, budget, out result))
        {
            return true;
        }

        if (Sign is not null && Sign == other.Sign)
        {
            result = new LinearDriftExactOffset(sumValue, Sign, null);
            return true;
        }

        // Do not publish a parity/zero theorem when cancellation between
        // unrelated symbolic constants has not itself been proved.
        result = default;
        return false;
    }

    private static bool TryClassifyStructured(ExactReal value, ResourceBudget budget, out LinearDriftExactOffset result)
    {
        switch (value)
        {
            case RationalReal rational:
                budget.CheckCoefficient(rational.Value);
                result = FromRational(rational.Value);
                return true;
            case AffinePiReal affine:
                budget.CheckCoefficient(affine.PiCoefficient);
                budget.CheckCoefficient(affine.Constant);
                if (affine.PiCoefficient.IsZero)
                {
                    result = FromRational(affine.Constant);
                    return true;
                }

                int? sign = affine.Constant.IsZero || affine.Constant.Sign == affine.PiCoefficient.Sign ? affine.PiCoefficient.Sign : null;
                // Irrationality of pi proves a*pi+b is nonzero whenever the
                // rational coefficients are not both zero, even if its sign
                // is not needed or determined here.
                result = new LinearDriftExactOffset(value, sign, null);
                return true;
            default:
                result = default;
                return false;
        }
    }
}
