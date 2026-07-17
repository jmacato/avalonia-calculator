using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record SparseMultivariatePolynomial
{
    private SparseMultivariatePolynomial(int variableCount, ImmutableSortedDictionary<Monomial, BigRational> terms)
    {
        VariableCount = variableCount;
        Terms = terms;
    }

    public int VariableCount { get; }
    public ImmutableSortedDictionary<Monomial, BigRational> Terms { get; }
    public bool IsZero => Terms.IsEmpty;
    public int TotalDegree => Terms.IsEmpty ? -1 : Terms.Keys.Max(static monomial => monomial.TotalDegree);

    public static SparseMultivariatePolynomial Create(int variableCount, IEnumerable<KeyValuePair<Monomial, BigRational>> terms, ResourceBudget budget)
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
                throw BudgetExceededException.ForLimit(nameof(AnalysisLimits.MultivariateDegree));
            }

            budget.CheckCoefficient(coefficient);
            if (!coefficient.IsZero)
            {
                result[monomial] = result.TryGetValue(monomial, out BigRational existing) ? existing + coefficient : coefficient;
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
            throw BudgetExceededException.ForLimit(nameof(AnalysisLimits.Monomials));
        }

        return new SparseMultivariatePolynomial(variableCount, result.ToImmutable());
    }

    public SparseMultivariatePolynomial Add(SparseMultivariatePolynomial other, ResourceBudget budget)
    {
        EnsureCompatible(other);
        return Create(VariableCount, Terms.Concat(other.Terms), budget);
    }

    public SparseMultivariatePolynomial Multiply(SparseMultivariatePolynomial other, ResourceBudget budget)
    {
        EnsureCompatible(other);
        var terms = new List<KeyValuePair<Monomial, BigRational>>();
        foreach ((Monomial leftMonomial, BigRational leftCoefficient) in Terms)
        {
            foreach ((Monomial rightMonomial, BigRational rightCoefficient) in other.Terms)
            {
                budget.Charge();
                terms.Add(new KeyValuePair<Monomial, BigRational>(leftMonomial.Multiply(rightMonomial), leftCoefficient * rightCoefficient));
            }
        }

        return Create(VariableCount, terms, budget);
    }

    public SparseMultivariatePolynomial Differentiate(int variable, ResourceBudget budget)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(variable, VariableCount);
        ArgumentOutOfRangeException.ThrowIfNegative(variable);
        return Create(VariableCount, Terms.Where(term => term.Key.Exponents[variable] > 0).Select(term => new KeyValuePair<Monomial, BigRational>(term.Key.Differentiate(variable), term.Value * term.Key.Exponents[variable])), budget);
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
