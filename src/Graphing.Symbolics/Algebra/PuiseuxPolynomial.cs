using System.Collections.Immutable;
using System.Text;

namespace Graphing.Symbolics;
/// <summary>
/// A finite rational-coefficient polynomial in rational powers of one
/// variable. The substitution x=t^L (or x=t^-L for a one-sided negative-power
/// phase), where L is the least common multiple of the exponent denominators,
/// lowers it to an ordinary exact polynomial. On the full real line this
/// coordinate is used only when every denominator is odd and the source
/// operators carry the signed odd-root branch.
/// </summary>
internal sealed class PuiseuxPolynomial
{
    private readonly ImmutableArray<PuiseuxTerm> _terms;
    private readonly bool _requiresNonnegativeVariable;
    private PuiseuxPolynomial(ImmutableArray<PuiseuxTerm> terms, bool requiresNonnegativeVariable = false)
    {
        _terms = terms;
        _requiresNonnegativeVariable = requiresNonnegativeVariable;
    }

    public ImmutableArray<PuiseuxTerm> Terms => _terms;
    public string Canonical => $"puiseux[{string.Join(',', _terms.Select(static term => term.Canonical))}]";
    public BigRational ConstantCoefficient => _terms.FirstOrDefault(static term => term.Exponent.IsZero).Coefficient;
    public BigRational LinearCoefficient => _terms.FirstOrDefault(static term => term.Exponent.IsOne).Coefficient;
    public bool IsConstant => _terms.All(static term => term.Exponent.IsZero);
    public bool IsAffine => _terms.All(static term => term.Exponent.IsZero || term.Exponent.IsOne);
    public bool IsOdd => !_requiresNonnegativeVariable && !_terms.IsEmpty && _terms.All(static term => !term.Exponent.IsZero && !term.Exponent.Denominator.IsEven && !term.Exponent.Numerator.IsEven);
    public bool IsEven => !_requiresNonnegativeVariable && !_terms.IsEmpty && _terms.All(static term => !term.Exponent.Denominator.IsEven && term.Exponent.Numerator.IsEven);

    public bool TryGetLeadingTerm(out PuiseuxTerm term)
    {
        if (_terms.IsEmpty)
        {
            term = default;
            return false;
        }

        term = _terms[^1];
        return true;
    }

    public static bool TryExtract(ValueTerm term, string variable, ResourceBudget budget, out PuiseuxPolynomial polynomial)
    {
        return TryExtractInCoordinate(term, variable, BigRational.Zero, budget, out polynomial);
    }

    /// <summary>
    /// Extracts the term after the exact affine coordinate change
    /// <c>x = u + variableOffset</c>.  This keeps shifted radical rays in the
    /// same Puiseux theorem instead of recognizing individual expressions.
    /// </summary>
    public static bool TryExtractInCoordinate(ValueTerm term, string variable, BigRational variableOffset, ResourceBudget budget, out PuiseuxPolynomial polynomial)
    {
        budget.Charge();
        switch (term.Kind)
        {
            case ValueKind.Constant:
                polynomial = Constant(term.Constant, budget);
                return true;
            case ValueKind.Variable when string.Equals(term.Name, variable, StringComparison.Ordinal):
                polynomial = Monomial(BigRational.One, BigRational.One, budget);
                if (!variableOffset.IsZero)
                {
                    polynomial = polynomial.Add(Constant(variableOffset, budget), budget);
                }

                return true;
            case ValueKind.Negate when term.Operands is [var operand]:
                return TryExtractNegation(operand, variable, variableOffset, budget, out polynomial);
            case ValueKind.Add or ValueKind.Subtract when term.Operands is [var left, var right]:
                return TryExtractAdditive(term.Kind, left, right, variable, variableOffset, budget, out polynomial);
            case ValueKind.Multiply when term.Operands is [var left, var right]:
                return TryExtractProduct(left, right, variable, variableOffset, budget, out polynomial);
            case ValueKind.Divide when term.Operands is [var numerator, var denominator]:
                return TryExtractQuotient(numerator, denominator, variable, variableOffset, budget, out polynomial);
            case ValueKind.Power when term.Operands is [var basis, var exponentTerm]:
                return TryExtractPower(basis, exponentTerm, variable, variableOffset, budget, out polynomial);
            case ValueKind.Function:
                return TryExtractRootFunction(term, variable, variableOffset, budget, out polynomial);
        }

        polynomial = null!;
        return false;
    }

    private static bool TryExtractNegation(ValueTerm operand, string variable, BigRational variableOffset, ResourceBudget budget, out PuiseuxPolynomial polynomial)
    {
        if (TryExtractInCoordinate(operand, variable, variableOffset, budget, out PuiseuxPolynomial extracted))
        {
            polynomial = extracted.Scale(BigRational.MinusOne, budget);
            return true;
        }

        polynomial = null!;
        return false;
    }

    private static bool TryExtractAdditive(ValueKind kind, ValueTerm left, ValueTerm right, string variable, BigRational variableOffset, ResourceBudget budget, out PuiseuxPolynomial polynomial)
    {
        if (TryExtractInCoordinate(left, variable, variableOffset, budget, out PuiseuxPolynomial leftPolynomial) && TryExtractInCoordinate(right, variable, variableOffset, budget, out PuiseuxPolynomial rightPolynomial))
        {
            polynomial = kind == ValueKind.Add ? leftPolynomial.Add(rightPolynomial, budget) : leftPolynomial.Subtract(rightPolynomial, budget);
            return true;
        }

        polynomial = null!;
        return false;
    }

    private static bool TryExtractProduct(ValueTerm left, ValueTerm right, string variable, BigRational variableOffset, ResourceBudget budget, out PuiseuxPolynomial polynomial)
    {
        if (TryExtractInCoordinate(left, variable, variableOffset, budget, out PuiseuxPolynomial leftPolynomial) && TryExtractInCoordinate(right, variable, variableOffset, budget, out PuiseuxPolynomial rightPolynomial))
        {
            polynomial = leftPolynomial.Multiply(rightPolynomial, budget);
            return true;
        }

        polynomial = null!;
        return false;
    }

    private static bool TryExtractQuotient(ValueTerm numerator, ValueTerm denominator, string variable, BigRational variableOffset, ResourceBudget budget, out PuiseuxPolynomial polynomial)
    {
        if (TryExtractInCoordinate(numerator, variable, variableOffset, budget, out PuiseuxPolynomial numeratorPolynomial) && TryExtractInCoordinate(denominator, variable, variableOffset, budget, out PuiseuxPolynomial denominatorPolynomial) && denominatorPolynomial.TryGetMonomial(out BigRational divisor, out BigRational divisorExponent) && !divisor.IsZero)
        {
            polynomial = numeratorPolynomial.Multiply(Monomial(divisor.Reciprocal(), -divisorExponent, budget), budget);
            return true;
        }

        polynomial = null!;
        return false;
    }

    private static bool TryExtractPower(ValueTerm basis, ValueTerm exponentTerm, string variable, BigRational variableOffset, ResourceBudget budget, out PuiseuxPolynomial polynomial)
    {
        if (TryExtractInCoordinate(basis, variable, variableOffset, budget, out PuiseuxPolynomial basisPolynomial) && ExactScalar.TryCreate(exponentTerm, budget, out ExactScalar exponentScalar) && exponentScalar.RationalValue is { } exponent)
        {
            return TryRaise(basisPolynomial, exponent, PuiseuxPolynomialRationalPowerBranch.Principal, budget, out polynomial);
        }

        polynomial = null!;
        return false;
    }

    private static bool TryExtractRootFunction(ValueTerm term, string variable, BigRational variableOffset, ResourceBudget budget, out PuiseuxPolynomial polynomial)
    {
        if (term is { Name: "sqrt", Operands: [var squareRadicand] } && TryExtractInCoordinate(squareRadicand, variable, variableOffset, budget, out PuiseuxPolynomial squarePolynomial))
        {
            return TryRaise(squarePolynomial, new BigRational(1, 2), PuiseuxPolynomialRationalPowerBranch.Principal, budget, out polynomial);
        }

        if (term is { Name: "root", Operands: [var radicand, var degreeTerm] } && TryExtractInCoordinate(radicand, variable, variableOffset, budget, out PuiseuxPolynomial radicandPolynomial) && ExactScalar.TryCreate(degreeTerm, budget, out ExactScalar degreeScalar) && degreeScalar.RationalValue is { IsInteger: true, Sign: > 0 } degree)
        {
            return TryRaise(radicandPolynomial, degree.Reciprocal(), degree.Numerator.IsEven ? PuiseuxPolynomialRationalPowerBranch.Principal : PuiseuxPolynomialRationalPowerBranch.SignedOddRoot, budget, out polynomial);
        }

        polynomial = null!;
        return false;
    }

    public bool TryLowerToPolynomial(bool nonnegativeDomain, ResourceBudget budget, out int substitutionDegree, out UnivariatePolynomial polynomial)
    {
        if (_terms.IsEmpty || !nonnegativeDomain && _requiresNonnegativeVariable)
        {
            substitutionDegree = default;
            polynomial = null!;
            return false;
        }

        bool hasNegativeExponent = _terms.Any(static term => term.Exponent.Sign < 0);
        bool hasPositiveExponent = _terms.Any(static term => term.Exponent.Sign > 0);
        if (hasNegativeExponent && (hasPositiveExponent || !nonnegativeDomain))
        {
            substitutionDegree = default;
            polynomial = null!;
            return false;
        }

        int coordinateSign = hasNegativeExponent ? -1 : 1;
        ExactInteger lcm = ExactInteger.One;
        foreach (PuiseuxTerm term in _terms)
        {
            budget.Charge();
            if (!nonnegativeDomain && !term.Exponent.IsInteger && term.Exponent.Denominator.IsEven)
            {
                substitutionDegree = default;
                polynomial = null!;
                return false;
            }

            lcm = Lcm(lcm, term.Exponent.Denominator);
            if (lcm > AnalysisLimits.UnivariateDegree)
            {
                throw new BudgetExceededException(nameof(AnalysisLimits.UnivariateDegree));
            }
        }

        substitutionDegree = checked(coordinateSign * (int)lcm);
        var degrees = new List<(int Degree, BigRational Coefficient)>(_terms.Length);
        int maximumDegree = 0;
        foreach (PuiseuxTerm term in _terms)
        {
            budget.Charge();
            BigRational lowered = term.Exponent * substitutionDegree;
            if (!lowered.IsInteger || lowered.Numerator < 0 || lowered.Numerator > AnalysisLimits.UnivariateDegree)
            {
                polynomial = null!;
                return false;
            }

            int degree = checked((int)lowered.Numerator);
            maximumDegree = Math.Max(maximumDegree, degree);
            degrees.Add((degree, term.Coefficient));
        }

        var coefficients = new BigRational[maximumDegree + 1];
        foreach ((int degree, BigRational coefficient) in degrees)
        {
            coefficients[degree] += coefficient;
        }

        polynomial = UnivariatePolynomial.Create(coefficients, budget);
        return !polynomial.IsZero;
    }

    public string Format(string variable)
    {
        return Format(_terms, variable, signedOddRoots: !_requiresNonnegativeVariable);
    }

    public static string Format(ImmutableArray<PuiseuxTerm> terms, string variable, bool signedOddRoots = false)
    {
        if (terms.IsEmpty)
        {
            return "0";
        }

        var builder = new StringBuilder();
        foreach (PuiseuxTerm term in terms.OrderByDescending(static term => term.Exponent))
        {
            int sign = term.Coefficient.Sign;
            BigRational magnitude = term.Coefficient.Abs();
            if (builder.Length > 0)
            {
                builder.Append(sign < 0 ? " − " : " + ");
            }
            else if (sign < 0)
            {
                builder.Append('−');
            }

            string power = FormatPower(variable, term.Exponent, signedOddRoots);
            if (term.Exponent.IsZero || !magnitude.IsOne)
            {
                builder.Append(magnitude);
                if (!term.Exponent.IsZero)
                {
                    builder.Append('*');
                }
            }

            if (!term.Exponent.IsZero)
            {
                builder.Append(power);
            }
        }

        return builder.ToString();
    }

    private static bool TryRaise(PuiseuxPolynomial basis, BigRational exponent, PuiseuxPolynomialRationalPowerBranch branch, ResourceBudget budget, out PuiseuxPolynomial result)
    {
        if (ExactInteger.Abs(exponent.Numerator) > AnalysisLimits.UnivariateDegree || exponent.Denominator > AnalysisLimits.UnivariateDegree)
        {
            result = null!;
            return false;
        }

        if (exponent.IsInteger && exponent.Sign >= 0 && exponent.Numerator <= AnalysisLimits.UnivariateDegree)
        {
            if (basis._terms is [var monomial] && !CoefficientPowerFitsLimit(monomial.Coefficient, exponent))
            {
                result = null!;
                return false;
            }

            result = basis.Pow(checked((int)exponent.Numerator), budget);
            return true;
        }

        if (basis._terms.IsEmpty && exponent.Sign > 0)
        {
            result = Constant(BigRational.Zero, budget);
            if (basis._requiresNonnegativeVariable)
            {
                result = result.WithNonnegativeVariableRequirement();
            }

            return true;
        }

        if (basis._terms.Length != 1)
        {
            result = null!;
            return false;
        }

        BigRational raisedExponent = basis._terms[0].Exponent * exponent;
        if (ExactInteger.Abs(raisedExponent.Numerator) > AnalysisLimits.UnivariateDegree || raisedExponent.Denominator > AnalysisLimits.UnivariateDegree)
        {
            result = null!;
            return false;
        }

        if (!TryRaiseRational(basis._terms[0].Coefficient, exponent, branch, budget, out BigRational coefficient))
        {
            result = null!;
            return false;
        }

        result = Monomial(coefficient, raisedExponent, budget);
        if (basis._requiresNonnegativeVariable || branch == PuiseuxPolynomialRationalPowerBranch.Principal && !exponent.IsInteger && basis._terms.Any(static term => !term.Exponent.IsZero))
        {
            // Replacing a rational power by x^(p/q) is branch-correct on
            // x >= 0. Preserve that precondition even when the resulting
            // exponent happens to simplify to an integer (sqrt(x^2)).
            result = result.WithNonnegativeVariableRequirement();
        }

        return true;
    }

    private static bool TryRaiseRational(BigRational basis, BigRational exponent, PuiseuxPolynomialRationalPowerBranch branch, ResourceBudget budget, out BigRational result)
    {
        if (exponent.IsInteger && exponent.Numerator >= int.MinValue && exponent.Numerator <= int.MaxValue)
        {
            int integerExponent = (int)exponent.Numerator;
            if ((basis.IsZero && integerExponent <= 0) || !CoefficientPowerFitsLimit(basis, exponent))
            {
                result = default;
                return false;
            }

            result = basis.Pow(integerExponent);
            budget.CheckCoefficient(result);
            return true;
        }

        if (!CoefficientPowerFitsLimit(basis, exponent))
        {
            result = default;
            return false;
        }

        bool signedNegative = basis.Sign < 0 && branch == PuiseuxPolynomialRationalPowerBranch.SignedOddRoot && !exponent.Denominator.IsEven;
        BigRational nonnegativeBasis = signedNegative ? -basis : basis;
        if (!FixedRationalPowerValue.TryComposeRational(nonnegativeBasis, exponent, budget, out result))
        {
            return false;
        }

        if (signedNegative && !exponent.Numerator.IsEven)
        {
            result = -result;
        }

        return true;
    }

    private static bool CoefficientPowerFitsLimit(BigRational basis, BigRational exponent)
    {
        if (exponent.IsZero || basis.Abs().IsOne)
        {
            return true;
        }

        if (basis.IsZero)
        {
            return exponent.Sign > 0;
        }

        long power = checked((long)ExactInteger.Abs(exponent.Numerator));
        long rootDegree = checked((long)exponent.Denominator);
        long numeratorBits = checked((long)ExactInteger.Abs(basis.Numerator).GetBitLength());
        long denominatorBits = checked((long)basis.Denominator.GetBitLength());
        long rootedNumeratorBits = checked((numeratorBits + rootDegree - 1) / rootDegree);
        long rootedDenominatorBits = checked((denominatorBits + rootDegree - 1) / rootDegree);
        return checked(rootedNumeratorBits * power) <= AnalysisLimits.CoefficientBits && checked(rootedDenominatorBits * power) <= AnalysisLimits.CoefficientBits;
    }

    private PuiseuxPolynomial Add(PuiseuxPolynomial other, ResourceBudget budget)
    {
        return Create(_terms.Concat(other._terms), budget,
            _requiresNonnegativeVariable || other._requiresNonnegativeVariable);
    }

    private PuiseuxPolynomial Subtract(PuiseuxPolynomial other, ResourceBudget budget)
    {
        return Add(other.Scale(BigRational.MinusOne, budget), budget);
    }

    private PuiseuxPolynomial Scale(BigRational scalar, ResourceBudget budget)
    {
        return Create(_terms.Select(term => term with { Coefficient = term.Coefficient * scalar }), budget,
            _requiresNonnegativeVariable);
    }

    private PuiseuxPolynomial Multiply(PuiseuxPolynomial other, ResourceBudget budget)
    {
        long productCount = checked((long)_terms.Length * other._terms.Length);
        budget.Charge(productCount);
        return Create(Products(_terms, other._terms), budget, _requiresNonnegativeVariable || other._requiresNonnegativeVariable);
    }

    private static IEnumerable<PuiseuxTerm> Products(ImmutableArray<PuiseuxTerm> leftTerms, ImmutableArray<PuiseuxTerm> rightTerms)
    {
        foreach (PuiseuxTerm left in leftTerms)
        {
            foreach (PuiseuxTerm right in rightTerms)
            {
                yield return new PuiseuxTerm(left.Exponent + right.Exponent, left.Coefficient * right.Coefficient);
            }
        }
    }

    private PuiseuxPolynomial Pow(int exponent, ResourceBudget budget)
    {
        PuiseuxPolynomial result = Constant(BigRational.One, budget);
        PuiseuxPolynomial factor = this;
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

    private bool TryGetConstant(out BigRational value)
    {
        if (_terms.Length == 0)
        {
            value = BigRational.Zero;
            return true;
        }

        if (_terms is [{ Exponent: { IsZero: true }, Coefficient: var constant }])
        {
            value = constant;
            return true;
        }

        value = default;
        return false;
    }

    private bool TryGetMonomial(out BigRational coefficient, out BigRational exponent)
    {
        if (_terms.IsEmpty)
        {
            coefficient = BigRational.Zero;
            exponent = BigRational.Zero;
            return true;
        }

        if (_terms is [var monomial])
        {
            coefficient = monomial.Coefficient;
            exponent = monomial.Exponent;
            return true;
        }

        coefficient = default;
        exponent = default;
        return false;
    }

    private static PuiseuxPolynomial Constant(BigRational value, ResourceBudget budget)
    {
        return value.IsZero ? new PuiseuxPolynomial([]) : Monomial(value, BigRational.Zero, budget);
    }

    private static PuiseuxPolynomial Monomial(BigRational coefficient, BigRational exponent, ResourceBudget budget)
    {
        return Create([new PuiseuxTerm(exponent, coefficient)], budget);
    }

    private static PuiseuxPolynomial Create(IEnumerable<PuiseuxTerm> terms, ResourceBudget budget, bool requiresNonnegativeVariable = false)
    {
        var combined = new SortedDictionary<BigRational, BigRational>();
        foreach (PuiseuxTerm term in terms)
        {
            budget.Charge();
            budget.CheckCoefficient(term.Exponent);
            budget.CheckCoefficient(term.Coefficient);
            BigRational combinedCoefficient = combined.TryGetValue(term.Exponent, out BigRational existing) ? existing + term.Coefficient : term.Coefficient;
            budget.CheckCoefficient(combinedCoefficient);
            if (combinedCoefficient.IsZero)
            {
                combined.Remove(term.Exponent);
            }
            else
            {
                combined[term.Exponent] = combinedCoefficient;
                if (combined.Count > AnalysisLimits.Monomials)
                {
                    throw new BudgetExceededException(nameof(AnalysisLimits.Monomials));
                }
            }
        }

        return new PuiseuxPolynomial(combined.Select(static pair => new PuiseuxTerm(pair.Key, pair.Value)).ToImmutableArray(), requiresNonnegativeVariable);
    }

    private PuiseuxPolynomial WithNonnegativeVariableRequirement()
    {
        return _requiresNonnegativeVariable ? this : new PuiseuxPolynomial(_terms, requiresNonnegativeVariable: true);
    }

    private static string FormatPower(string variable, BigRational exponent, bool signedOddRoots)
    {
        if (exponent.IsOne)
        {
            return variable;
        }

        if (exponent == new BigRational(1, 2))
        {
            return $"sqrt({variable})";
        }

        if (exponent.IsInteger)
        {
            return $"{variable}^{exponent.Numerator}";
        }

        if (signedOddRoots && !exponent.Denominator.IsEven)
        {
            string root = $"root({variable}, {exponent.Denominator})";
            return exponent.Numerator.IsOne ? root : $"({root})^{exponent.Numerator}";
        }

        return $"{variable}^({exponent})";
    }

    private static ExactInteger Lcm(ExactInteger left, ExactInteger right)
    {
        return ExactInteger.Abs(left / ExactInteger.GreatestCommonDivisor(left, right) * right);
    }
}
