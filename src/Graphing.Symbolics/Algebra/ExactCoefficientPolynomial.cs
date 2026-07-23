using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// A deliberately bounded polynomial over proved exact real scalars.  This is
/// not the rational polynomial ring used by Sturm/CAD: every coefficient
/// operation must retain a proved sign, otherwise extraction fails and the
/// caller returns Unknown.
/// </summary>
internal sealed record ExactCoefficientPolynomial
{
    private ExactCoefficientPolynomial(ImmutableArray<ExactScalar> coefficients)
    {
        Coefficients = Trim(coefficients);
    }

    public ImmutableArray<ExactScalar> Coefficients { get; }
    public int Degree => Coefficients.Length - 1;
    public bool IsZero => Degree == 0 && this[0].IsZero;
    public bool IsConstant => Degree == 0;
    public bool HasNonRationalCoefficient => Coefficients.Any(static coefficient => coefficient.RationalValue is null);
    public string Canonical => $"exact-poly[{string.Join(',', Coefficients.Select(static coefficient => coefficient.Canonical))}]";

    public ExactScalar this[int degree] => degree < 0 || degree >= Coefficients.Length ? ExactScalar.Zero : Coefficients[degree];
    public static ExactCoefficientPolynomial Constant(ExactScalar value)
    {
        return new ExactCoefficientPolynomial([value]);
    }

    public static ExactCoefficientPolynomial Variable { get; } = new([ExactScalar.Zero, ExactScalar.One]);

    public static bool TryAdd(ExactCoefficientPolynomial left, ExactCoefficientPolynomial right, ResourceBudget budget, out ExactCoefficientPolynomial result)
    {
        int length = Math.Max(left.Coefficients.Length, right.Coefficients.Length);
        var coefficients = ImmutableArray.CreateBuilder<ExactScalar>(length);
        for (int degree = 0; degree < length; degree++)
        {
            budget.Charge();
            if (!ExactScalar.TryAdd(left[degree], right[degree], budget, out ExactScalar sum))
            {
                result = null!;
                return false;
            }

            coefficients.Add(sum);
        }

        result = new ExactCoefficientPolynomial(coefficients.MoveToImmutable());
        return CheckLimits(result, budget);
    }

    public static bool TrySubtract(ExactCoefficientPolynomial left, ExactCoefficientPolynomial right, ResourceBudget budget, out ExactCoefficientPolynomial result)
    {
        return TryAdd(left, right.Negate(budget), budget, out result);
    }

    public static bool TryMultiply(ExactCoefficientPolynomial left, ExactCoefficientPolynomial right, ResourceBudget budget, out ExactCoefficientPolynomial result)
    {
        int degree = checked(left.Degree + right.Degree);
        if (degree > AnalysisLimits.UnivariateDegree)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.UnivariateDegree));
        }

        var coefficients = Enumerable.Repeat(ExactScalar.Zero, degree + 1).ToArray();
        for (int i = 0; i <= left.Degree; i++)
        {
            for (int j = 0; j <= right.Degree; j++)
            {
                budget.Charge();
                ExactScalar product = left[i].Multiply(right[j], budget);
                if (!ExactScalar.TryAdd(coefficients[i + j], product, budget, out coefficients[i + j]))
                {
                    result = null!;
                    return false;
                }
            }
        }

        result = new ExactCoefficientPolynomial(coefficients.ToImmutableArray());
        return CheckLimits(result, budget);
    }

    public ExactCoefficientPolynomial Negate(ResourceBudget budget)
    {
        var coefficients = ImmutableArray.CreateBuilder<ExactScalar>(Coefficients.Length);
        foreach (ExactScalar coefficient in Coefficients)
        {
            budget.Charge();
            coefficients.Add(coefficient.Negate());
        }

        return new ExactCoefficientPolynomial(coefficients.MoveToImmutable());
    }

    public ExactCoefficientPolynomial Scale(ExactScalar scalar, ResourceBudget budget)
    {
        var coefficients = ImmutableArray.CreateBuilder<ExactScalar>(Coefficients.Length);
        foreach (ExactScalar coefficient in Coefficients)
        {
            budget.Charge();
            coefficients.Add(coefficient.Multiply(scalar, budget));
        }

        return new ExactCoefficientPolynomial(coefficients.MoveToImmutable());
    }

    public bool TryEvaluate(ExactScalar argument, ResourceBudget budget, out ExactScalar value)
    {
        value = ExactScalar.Zero;
        for (int degree = Degree; degree >= 0; degree--)
        {
            budget.Charge();
            ExactScalar product = value.Multiply(argument, budget);
            if (!ExactScalar.TryAdd(product, this[degree], budget, out value))
            {
                value = default;
                return false;
            }
        }

        return true;
    }

    public bool EqualsPolynomial(ExactCoefficientPolynomial other)
    {
        if (Degree != other.Degree)
        {
            return false;
        }

        for (int degree = 0; degree <= Degree; degree++)
        {
            if (!string.Equals(this[degree].Canonical, other[degree].Canonical, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static ImmutableArray<ExactScalar> Trim(ImmutableArray<ExactScalar> coefficients)
    {
        int length = coefficients.Length;
        while (length > 1 && coefficients[length - 1].IsZero)
        {
            length--;
        }

        return length == 0 ? [ExactScalar.Zero] : coefficients[..length];
    }

    private static bool CheckLimits(ExactCoefficientPolynomial polynomial, ResourceBudget budget)
    {
        if (polynomial.Degree > AnalysisLimits.UnivariateDegree)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.UnivariateDegree));
        }

        if (polynomial.Coefficients.Count(static coefficient => !coefficient.IsZero) > AnalysisLimits.Monomials)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.Monomials));
        }

        foreach (ExactScalar coefficient in polynomial.Coefficients)
        {
            budget.Charge();
            if (coefficient.RationalValue is { } rational)
            {
                budget.CheckCoefficient(rational);
            }
        }

        return true;
    }
}
