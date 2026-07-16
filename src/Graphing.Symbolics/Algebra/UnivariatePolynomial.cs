using System.Collections.Immutable;
using System.Numerics;
using System.Text;

namespace Graphing.Symbolics;

internal sealed class UnivariatePolynomial : IEquatable<UnivariatePolynomial>
{
    private readonly ImmutableArray<BigRational> _coefficients;

    private UnivariatePolynomial(ImmutableArray<BigRational> coefficients)
    {
        _coefficients = coefficients;
    }

    public static UnivariatePolynomial Zero { get; } = new([]);

    public static UnivariatePolynomial One { get; } = new([BigRational.One]);

    public static UnivariatePolynomial Variable { get; } = new([BigRational.Zero, BigRational.One]);

    public int Degree => _coefficients.Length - 1;

    public bool IsZero => _coefficients.IsEmpty;

    public bool IsConstant => Degree <= 0;

    public BigRational LeadingCoefficient => IsZero ? BigRational.Zero : _coefficients[^1];

    public BigRational ConstantCoefficient => IsZero ? BigRational.Zero : _coefficients[0];

    public BigRational this[int degree] => degree >= 0 && degree < _coefficients.Length
        ? _coefficients[degree]
        : BigRational.Zero;

    public ImmutableArray<BigRational> Coefficients => _coefficients;

    public static UnivariatePolynomial Create(
        IEnumerable<BigRational> coefficients,
        ResourceBudget budget)
    {
        ImmutableArray<BigRational> materialized = coefficients.ToImmutableArray();
        int length = materialized.Length;
        while (length > 0 && materialized[length - 1].IsZero)
        {
            length--;
        }

        if (length == 0)
        {
            return Zero;
        }

        if (length - 1 > AnalysisLimits.UnivariateDegree)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.UnivariateDegree));
        }

        if (length > AnalysisLimits.Monomials)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.Monomials));
        }

        var normalized = ImmutableArray.CreateBuilder<BigRational>(length);
        for (int index = 0; index < length; index++)
        {
            BigRational coefficient = materialized[index];
            budget.CheckCoefficient(coefficient);
            normalized.Add(coefficient);
        }

        return new UnivariatePolynomial(normalized.MoveToImmutable());
    }

    public UnivariatePolynomial Add(UnivariatePolynomial other, ResourceBudget budget)
    {
        int length = Math.Max(_coefficients.Length, other._coefficients.Length);
        var result = new BigRational[length];
        for (int index = 0; index < length; index++)
        {
            budget.Charge();
            result[index] = this[index] + other[index];
        }

        return Create(result, budget);
    }

    public UnivariatePolynomial Subtract(UnivariatePolynomial other, ResourceBudget budget)
    {
        int length = Math.Max(_coefficients.Length, other._coefficients.Length);
        var result = new BigRational[length];
        for (int index = 0; index < length; index++)
        {
            budget.Charge();
            result[index] = this[index] - other[index];
        }

        return Create(result, budget);
    }

    public UnivariatePolynomial Negate(ResourceBudget budget)
    {
        var result = new BigRational[_coefficients.Length];
        for (int index = 0; index < result.Length; index++)
        {
            budget.Charge();
            result[index] = -_coefficients[index];
        }

        return Create(result, budget);
    }

    public UnivariatePolynomial Multiply(UnivariatePolynomial other, ResourceBudget budget)
    {
        if (IsZero || other.IsZero)
        {
            return Zero;
        }

        int degree = checked(Degree + other.Degree);
        if (degree > AnalysisLimits.UnivariateDegree)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.UnivariateDegree));
        }

        var result = new BigRational[degree + 1];
        for (int left = 0; left < _coefficients.Length; left++)
        {
            for (int right = 0; right < other._coefficients.Length; right++)
            {
                budget.Charge();
                result[left + right] += _coefficients[left] * other._coefficients[right];
            }
        }

        return Create(result, budget);
    }

    public UnivariatePolynomial Multiply(BigRational scalar, ResourceBudget budget)
    {
        if (scalar.IsZero || IsZero)
        {
            return Zero;
        }

        var result = new BigRational[_coefficients.Length];
        for (int index = 0; index < result.Length; index++)
        {
            budget.Charge();
            result[index] = _coefficients[index] * scalar;
        }

        return Create(result, budget);
    }

    public UnivariatePolynomial Pow(int exponent, ResourceBudget budget)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(exponent);
        UnivariatePolynomial result = One;
        UnivariatePolynomial factor = this;
        int remaining = exponent;
        while (remaining > 0)
        {
            budget.Charge();
            if ((remaining & 1) != 0)
            {
                result = result.Multiply(factor, budget);
            }

            remaining >>= 1;
            if (remaining > 0)
            {
                factor = factor.Multiply(factor, budget);
            }
        }

        return result;
    }

    public UnivariatePolynomial Derivative(ResourceBudget budget)
    {
        if (Degree <= 0)
        {
            return Zero;
        }

        var result = new BigRational[Degree];
        for (int degree = 1; degree < _coefficients.Length; degree++)
        {
            budget.Charge();
            result[degree - 1] = _coefficients[degree] * degree;
        }

        return Create(result, budget);
    }

    public BigRational Evaluate(BigRational value, ResourceBudget budget)
    {
        BigRational result = BigRational.Zero;
        for (int degree = Degree; degree >= 0; degree--)
        {
            budget.Charge();
            result = (result * value) + _coefficients[degree];
            budget.CheckCoefficient(result);
        }

        return result;
    }

    public (UnivariatePolynomial Quotient, UnivariatePolynomial Remainder) Divide(
        UnivariatePolynomial divisor,
        ResourceBudget budget)
    {
        if (divisor.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (IsZero || Degree < divisor.Degree)
        {
            return (Zero, this);
        }

        var remainder = _coefficients.ToArray();
        var quotient = new BigRational[Degree - divisor.Degree + 1];
        for (int degree = Degree; degree >= divisor.Degree; degree--)
        {
            budget.Charge();
            BigRational factor = remainder[degree] / divisor.LeadingCoefficient;
            int shift = degree - divisor.Degree;
            quotient[shift] = factor;
            for (int index = 0; index <= divisor.Degree; index++)
            {
                budget.Charge();
                remainder[index + shift] -= factor * divisor[index];
            }
        }

        return (Create(quotient, budget), Create(remainder, budget));
    }

    public UnivariatePolynomial Monic(ResourceBudget budget) => IsZero
        ? Zero
        : Multiply(LeadingCoefficient.Reciprocal(), budget);

    public UnivariatePolynomial PrimitivePositive(ResourceBudget budget)
    {
        if (IsZero)
        {
            return Zero;
        }

        BigInteger denominatorLcm = BigInteger.One;
        foreach (BigRational coefficient in _coefficients)
        {
            budget.Charge();
            denominatorLcm = Lcm(denominatorLcm, coefficient.Denominator);
        }

        BigInteger content = BigInteger.Zero;
        var integers = new BigInteger[_coefficients.Length];
        for (int index = 0; index < integers.Length; index++)
        {
            budget.Charge();
            integers[index] = _coefficients[index].Numerator * (denominatorLcm / _coefficients[index].Denominator);
            content = BigInteger.GreatestCommonDivisor(content, BigInteger.Abs(integers[index]));
        }

        if (integers[^1].Sign < 0)
        {
            content = BigInteger.Negate(content);
        }

        return Create(integers.Select(value => new BigRational(value / content)), budget);
    }

    public UnivariatePolynomial SubstituteNegativeVariable(ResourceBudget budget)
    {
        var coefficients = _coefficients.ToArray();
        for (int degree = 1; degree < coefficients.Length; degree += 2)
        {
            budget.Charge();
            coefficients[degree] = -coefficients[degree];
        }

        return Create(coefficients, budget);
    }

    public static UnivariatePolynomial GreatestCommonDivisor(
        UnivariatePolynomial left,
        UnivariatePolynomial right,
        ResourceBudget budget)
    {
        UnivariatePolynomial a = left;
        UnivariatePolynomial b = right;
        while (!b.IsZero)
        {
            budget.Charge();
            (_, UnivariatePolynomial remainder) = a.Divide(b, budget);
            a = b;
            b = remainder;
        }

        return a.Monic(budget);
    }

    public UnivariatePolynomial SquareFreePart(ResourceBudget budget)
    {
        if (Degree <= 0)
        {
            return this;
        }

        UnivariatePolynomial gcd = GreatestCommonDivisor(this, Derivative(budget), budget);
        (UnivariatePolynomial quotient, UnivariatePolynomial remainder) = Divide(gcd, budget);
        if (!remainder.IsZero)
        {
            throw new InvalidOperationException("The exact polynomial gcd did not divide its input.");
        }

        return quotient.Monic(budget);
    }

    public bool Equals(UnivariatePolynomial? other)
    {
        if (other is null || _coefficients.Length != other._coefficients.Length)
        {
            return false;
        }

        for (int index = 0; index < _coefficients.Length; index++)
        {
            if (_coefficients[index] != other._coefficients[index])
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as UnivariatePolynomial);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (BigRational coefficient in _coefficients)
        {
            hash.Add(coefficient);
        }

        return hash.ToHashCode();
    }

    public string Canonical
    {
        get
        {
            var builder = new StringBuilder("poly[");
            for (int index = 0; index < _coefficients.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append(_coefficients[index]);
            }

            return builder.Append(']').ToString();
        }
    }

    public override string ToString() => Canonical;

    private static BigInteger Lcm(BigInteger left, BigInteger right) =>
        BigInteger.Abs((left / BigInteger.GreatestCommonDivisor(left, right)) * right);
}
