using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed class Monomial : IEquatable<Monomial>, IComparable<Monomial>
{
    public Monomial(IEnumerable<int> exponents)
    {
        Exponents = exponents.ToImmutableArray();
        if (Exponents.Any(static exponent => exponent < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(exponents));
        }
    }

    public ImmutableArray<int> Exponents { get; }

    public int TotalDegree
    {
        get
        {
            int degree = 0;
            foreach (int exponent in Exponents)
            {
                degree = checked(degree + exponent);
            }

            return degree;
        }
    }

    public Monomial Multiply(Monomial other)
    {
        if (Exponents.Length != other.Exponents.Length)
        {
            throw new ArgumentException("Monomials must use the same variable count.", nameof(other));
        }

        return new Monomial(Exponents.Zip(other.Exponents, checked((left, right) => left + right)));
    }

    public Monomial Differentiate(int variable)
    {
        int[] exponents = Exponents.ToArray();
        exponents[variable]--;
        return new Monomial(exponents);
    }

    public int CompareTo(Monomial? other)
    {
        if (other is null)
        {
            return 1;
        }

        int degree = TotalDegree.CompareTo(other.TotalDegree);
        if (degree != 0)
        {
            return degree;
        }

        int length = Exponents.Length.CompareTo(other.Exponents.Length);
        if (length != 0)
        {
            return length;
        }

        for (int index = 0; index < Exponents.Length; index++)
        {
            int comparison = Exponents[index].CompareTo(other.Exponents[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    public bool Equals(Monomial? other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => Equals(obj as Monomial);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (int exponent in Exponents)
        {
            hash.Add(exponent);
        }

        return hash.ToHashCode();
    }
}

internal sealed record SparseMultivariatePolynomial
{
    private SparseMultivariatePolynomial(
        int variableCount,
        ImmutableSortedDictionary<Monomial, BigRational> terms)
    {
        VariableCount = variableCount;
        Terms = terms;
    }

    public int VariableCount { get; }

    public ImmutableSortedDictionary<Monomial, BigRational> Terms { get; }

    public bool IsZero => Terms.IsEmpty;

    public int TotalDegree => Terms.IsEmpty ? -1 : Terms.Keys.Max(static monomial => monomial.TotalDegree);

    public static SparseMultivariatePolynomial Create(
        int variableCount,
        IEnumerable<KeyValuePair<Monomial, BigRational>> terms,
        ResourceBudget budget)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(variableCount);
        var result = ImmutableSortedDictionary.CreateBuilder<Monomial, BigRational>();
        foreach ((Monomial monomial, BigRational coefficient) in terms)
        {
            budget.Charge();
            if (monomial.Exponents.Length != variableCount)
            {
                throw new ArgumentException("A monomial has the wrong variable count.", nameof(terms));
            }

            if (monomial.TotalDegree > AnalysisLimits.MultivariateDegree)
            {
                throw new BudgetExceededException(nameof(AnalysisLimits.MultivariateDegree));
            }

            budget.CheckCoefficient(coefficient);
            if (!coefficient.IsZero)
            {
                result[monomial] = result.TryGetValue(monomial, out BigRational existing)
                    ? existing + coefficient
                    : coefficient;
                if (result[monomial].IsZero)
                {
                    result.Remove(monomial);
                }
                else
                {
                    budget.CheckCoefficient(result[monomial]);
                }
            }
        }

        if (result.Count > AnalysisLimits.Monomials)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.Monomials));
        }

        return new SparseMultivariatePolynomial(variableCount, result.ToImmutable());
    }

    public SparseMultivariatePolynomial Add(
        SparseMultivariatePolynomial other,
        ResourceBudget budget)
    {
        EnsureCompatible(other);
        return Create(VariableCount, Terms.Concat(other.Terms), budget);
    }

    public SparseMultivariatePolynomial Multiply(
        SparseMultivariatePolynomial other,
        ResourceBudget budget)
    {
        EnsureCompatible(other);
        var terms = new List<KeyValuePair<Monomial, BigRational>>();
        foreach ((Monomial leftMonomial, BigRational leftCoefficient) in Terms)
        {
            foreach ((Monomial rightMonomial, BigRational rightCoefficient) in other.Terms)
            {
                budget.Charge();
                terms.Add(new KeyValuePair<Monomial, BigRational>(
                    leftMonomial.Multiply(rightMonomial),
                    leftCoefficient * rightCoefficient));
            }
        }

        return Create(VariableCount, terms, budget);
    }

    public SparseMultivariatePolynomial Differentiate(int variable, ResourceBudget budget)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(variable, VariableCount);
        ArgumentOutOfRangeException.ThrowIfNegative(variable);
        return Create(
            VariableCount,
            Terms
                .Where(term => term.Key.Exponents[variable] > 0)
                .Select(term => new KeyValuePair<Monomial, BigRational>(
                    term.Key.Differentiate(variable),
                    term.Value * term.Key.Exponents[variable])),
            budget);
    }

    public BigRational Evaluate(ImmutableArray<BigRational> values, ResourceBudget budget)
    {
        if (values.Length != VariableCount)
        {
            throw new ArgumentException("The evaluation point has the wrong dimension.", nameof(values));
        }

        BigRational result = BigRational.Zero;
        foreach ((Monomial monomial, BigRational coefficient) in Terms)
        {
            BigRational term = coefficient;
            for (int variable = 0; variable < VariableCount; variable++)
            {
                budget.Charge();
                term *= values[variable].Pow(monomial.Exponents[variable]);
            }

            result += term;
        }

        return result;
    }

    private void EnsureCompatible(SparseMultivariatePolynomial other)
    {
        if (VariableCount != other.VariableCount)
        {
            throw new ArgumentException("Polynomials must use the same variable count.", nameof(other));
        }
    }
}
