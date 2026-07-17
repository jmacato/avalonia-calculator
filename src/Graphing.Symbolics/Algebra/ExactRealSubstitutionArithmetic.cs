using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Exact, sign-optional arithmetic used after substituting a rational value
/// into a semantic term. The normal forms are structural identities over the
/// whole expression tree; they do not recognize source-expression shapes.
/// </summary>
internal static class ExactRealSubstitutionArithmetic
{
    public static ExactReal ValidateCoefficients(ExactReal value, ResourceBudget budget)
    {
        var pending = new Stack<ExactReal>();
        var visited = new HashSet<ExactReal>(ReferenceEqualityComparer.Instance);
        pending.Push(value);
        while (pending.TryPop(out ExactReal? current))
        {
            budget.Charge();
            if (!visited.Add(current))
            {
                continue;
            }

            switch (current)
            {
                case RationalReal rational:
                    budget.CheckCoefficient(rational.Value);
                    break;
                case AffinePiReal affine:
                    budget.CheckCoefficient(affine.PiCoefficient);
                    budget.CheckCoefficient(affine.Constant);
                    break;
                case AlgebraicReal algebraic:
                    CheckPolynomial(algebraic.Polynomial, budget);
                    budget.CheckCoefficient(algebraic.IsolatingInterval.Lower);
                    budget.CheckCoefficient(algebraic.IsolatingInterval.Upper);
                    break;
                case FunctionReal function:
                    foreach (ExactReal argument in function.Arguments)
                    {
                        pending.Push(argument);
                    }

                    break;
                case AlgebraicImageReal image:
                    CheckPolynomial(image.Function.Numerator, budget);
                    CheckPolynomial(image.Function.Denominator, budget);
                    pending.Push(image.Argument);
                    break;
            }
        }

        return value;
    }

    public static ExactReal Negate(ExactReal value, ResourceBudget budget) => Scale(value, BigRational.MinusOne, budget);
    public static ExactReal Add(ExactReal left, ExactReal right, ResourceBudget budget)
    {
        budget.Charge();
        var terms = new SortedDictionary<string, ExactRealSubstitutionArithmeticLinearTerm>(StringComparer.Ordinal);
        BigRational rational = BigRational.Zero;
        BigRational piCoefficient = BigRational.Zero;
        CollectAddend(left, BigRational.One, terms, ref rational, ref piCoefficient, budget);
        CollectAddend(right, BigRational.One, terms, ref rational, ref piCoefficient, budget);
        ExactReal? sum = null;
        foreach (ExactRealSubstitutionArithmeticLinearTerm term in terms.Values)
        {
            if (term.Coefficient.IsZero)
            {
                continue;
            }

            ExactReal addend = ScalePrimitive(term.Basis, term.Coefficient, budget);
            sum = sum is null ? addend : Binary("add", sum, addend);
        }

        if (!piCoefficient.IsZero)
        {
            ExactReal affine = new AffinePiReal(Checked(piCoefficient, budget), Checked(rational, budget));
            sum = sum is null ? affine : Binary("add", sum, affine);
            rational = BigRational.Zero;
        }

        if (!rational.IsZero)
        {
            ExactReal constant = new RationalReal(Checked(rational, budget));
            sum = sum is null ? constant : Binary("add", sum, constant);
        }

        return sum ?? new RationalReal(BigRational.Zero);
    }

    public static ExactReal Subtract(ExactReal left, ExactReal right, ResourceBudget budget) => Add(left, Negate(right, budget), budget);
    public static ExactReal Multiply(ExactReal left, ExactReal right, ResourceBudget budget)
    {
        budget.Charge();
        BigRational coefficient = BigRational.One;
        var factors = new List<ExactReal>();
        CollectFactor(left, ref coefficient, factors, budget);
        CollectFactor(right, ref coefficient, factors, budget);
        coefficient = Checked(coefficient, budget);
        if (coefficient.IsZero)
        {
            return new RationalReal(BigRational.Zero);
        }

        factors.Sort(static (first, second) => StringComparer.Ordinal.Compare(ExactRealCanonical.Format(first), ExactRealCanonical.Format(second)));
        ExactReal? product = null;
        foreach (ExactReal factor in factors)
        {
            product = product is null ? factor : Binary("multiply", product, factor);
        }

        return product is null ? new RationalReal(coefficient) : ScalePrimitive(product, coefficient, budget);
    }

    public static ExactReal Divide(ExactReal numerator, ExactReal denominator, ResourceBudget budget)
    {
        budget.Charge();
        if (denominator is RationalReal rational)
        {
            if (rational.Value.IsZero)
            {
                throw new DivideByZeroException();
            }

            return Scale(numerator, rational.Value.Reciprocal(), budget);
        }

        if (numerator is RationalReal { Value.IsZero: true })
        {
            return numerator;
        }

        if (string.Equals(ExactRealCanonical.Format(numerator), ExactRealCanonical.Format(denominator), StringComparison.Ordinal))
        {
            return new RationalReal(BigRational.One);
        }

        return Binary("divide", numerator, denominator);
    }

    public static ExactReal Scale(ExactReal value, BigRational factor, ResourceBudget budget)
    {
        budget.Charge();
        return ScalePrimitive(value, Checked(factor, budget), budget);
    }

    public static ExactReal Power(ExactReal basis, BigRational exponent, ResourceBudget budget)
    {
        budget.Charge();
        budget.CheckCoefficient(exponent);
        if (exponent.IsZero)
        {
            return new RationalReal(BigRational.One);
        }

        if (exponent.IsOne)
        {
            return basis;
        }

        if (basis is RationalReal rational)
        {
            if (rational.Value.IsOne)
            {
                return rational;
            }

            if (rational.Value.IsZero && exponent.Sign > 0)
            {
                return rational;
            }

            if (exponent.IsInteger && exponent.Numerator >= int.MinValue && exponent.Numerator <= int.MaxValue)
            {
                int integerExponent = (int)exponent.Numerator;
                PreflightPower(rational.Value, integerExponent);
                return new RationalReal(Checked(rational.Value.Pow(integerExponent), budget));
            }

            if (rational.Value.Sign >= 0 && exponent.Numerator >= int.MinValue && exponent.Numerator <= int.MaxValue && exponent.Denominator <= int.MaxValue)
            {
                return FixedRationalPowerValue.Compose(rational, exponent, budget);
            }
        }

        if (exponent == BigRational.MinusOne)
        {
            return Divide(new RationalReal(BigRational.One), basis, budget);
        }

        return new FunctionReal("power", [basis, new RationalReal(exponent)]);
    }

    public static ExactReal SquareRoot(ExactReal radicand, ResourceBudget budget)
    {
        budget.Charge();
        if (radicand is RationalReal rational && BigRational.TrySquareRoot(rational.Value, out BigRational root))
        {
            return new RationalReal(Checked(root, budget));
        }

        return radicand is RationalReal { Value.IsZero: true } ? radicand : new FunctionReal("sqrt", [radicand]);
    }

    public static ExactReal Root(ExactReal radicand, BigRational degree, ResourceBudget budget)
    {
        budget.Charge();
        budget.CheckCoefficient(degree);
        if (degree.IsOne)
        {
            return radicand;
        }

        if (degree == BigRational.MinusOne)
        {
            if (radicand is RationalReal { Value.IsZero: true })
            {
                // The caller may be evaluating a parent guard before the
                // conjunction reaches this root's own false domain guard.
                // Preserve the undefined term symbolically; never turn guard
                // ordering into a divide-by-zero exception.
                return new FunctionReal("root", [radicand, new RationalReal(degree)]);
            }

            return Divide(new RationalReal(BigRational.One), radicand, budget);
        }

        if (degree == new BigRational(2))
        {
            return SquareRoot(radicand, budget);
        }

        if (degree.IsInteger && degree.Numerator >= int.MinValue && degree.Numerator <= int.MaxValue && radicand is RationalReal rational)
        {
            BigRational exponent = BigRational.One / degree;
            ExactReal magnitude = rational.Value.Sign < 0 ? FixedRationalPowerValue.Compose(new RationalReal(rational.Value.Abs()), exponent, budget) : FixedRationalPowerValue.Compose(rational, exponent, budget);
            if (magnitude is RationalReal && rational.Value.Sign >= 0)
            {
                return magnitude;
            }

            if (magnitude is RationalReal && !degree.Numerator.IsEven)
            {
                return Negate(magnitude, budget);
            }
        }

        return new FunctionReal("root", [radicand, new RationalReal(degree)]);
    }

    public static ExactReal Absolute(ExactReal value, ResourceBudget budget)
    {
        budget.Charge();
        if (TrySign(value, budget, out int sign))
        {
            return sign < 0 ? Negate(value, budget) : value;
        }

        return new FunctionReal("abs", [value]);
    }

    public static bool TryCompare(ExactReal left, Comparison comparison, ExactReal right, ResourceBudget budget, out bool result)
    {
        budget.Charge();
        int order;
        if (string.Equals(ExactRealCanonical.Format(left), ExactRealCanonical.Format(right), StringComparison.Ordinal))
        {
            order = 0;
        }
        else if (left is RationalReal leftRational && right is RationalReal rightRational)
        {
            order = leftRational.Value.CompareTo(rightRational.Value);
        }
        else if (!TrySign(Subtract(left, right, budget), budget, out order))
        {
            result = false;
            return false;
        }

        result = comparison switch
        {
            Comparison.Equal => order == 0,
            Comparison.NotEqual => order != 0,
            Comparison.Less => order < 0,
            Comparison.LessOrEqual => order <= 0,
            Comparison.Greater => order > 0,
            Comparison.GreaterOrEqual => order >= 0,
            _ => throw new ArgumentOutOfRangeException(nameof(comparison))
        };
        return true;
    }

    public static bool TrySign(ExactReal value, ResourceBudget budget, out int sign)
    {
        budget.Charge();
        switch (value)
        {
            case RationalReal rational:
                sign = rational.Value.Sign;
                return true;
            case AlgebraicReal algebraic when algebraic.IsolatingInterval.Lower.Sign >= 0:
                // Algebraic interval endpoints are certified not to be roots;
                // a zero lower endpoint therefore still places the root
                // strictly on the positive side.
                sign = 1;
                return true;
            case AlgebraicReal algebraic when algebraic.IsolatingInterval.Upper.Sign <= 0:
                sign = -1;
                return true;
            case NamedReal { Name: "e" }:
                sign = 1;
                return true;
            case FunctionReal function:
                return TryFunctionSign(function, budget, out sign);
        }

        if (ExactScalarOrder.TryEnclose(value, budget, out RationalEnclosure enclosure))
        {
            if (enclosure.Lower > BigRational.Zero)
            {
                sign = 1;
                return true;
            }

            if (enclosure.Upper < BigRational.Zero)
            {
                sign = -1;
                return true;
            }

            if (enclosure.IsExact && enclosure.Lower.IsZero)
            {
                sign = 0;
                return true;
            }
        }

        sign = default;
        return false;
    }

    private static bool TryFunctionSign(FunctionReal function, ResourceBudget budget, out int sign)
    {
        switch (function)
        {
            case { Function: "negate", Arguments: [var operand] } when TrySign(operand, budget, out int operandSign):
                sign = -operandSign;
                return true;
            case { Function: "scale", Arguments: [var operand, RationalReal factor] } when TrySign(operand, budget, out int scaledSign):
                sign = scaledSign * factor.Value.Sign;
                return true;
            case { Function: "multiply", Arguments: [var left, var right] } when TrySign(left, budget, out int leftSign) && TrySign(right, budget, out int rightSign):
                sign = leftSign * rightSign;
                return true;
            case { Function: "divide", Arguments: [var numerator, var denominator] } when TrySign(numerator, budget, out int numeratorSign) && TrySign(denominator, budget, out int denominatorSign) && denominatorSign != 0:
                sign = numeratorSign * denominatorSign;
                return true;
            case { Function: "sqrt", Arguments: [var radicand] } when TrySign(radicand, budget, out int radicandSign) && radicandSign >= 0:
                sign = radicandSign;
                return true;
            case { Function: "abs", Arguments: [var absolute] } when TrySign(absolute, budget, out int absoluteSign):
                sign = absoluteSign == 0 ? 0 : 1;
                return true;
            case { Function: "exp", Arguments.Length: 1 }:
                sign = 1;
                return true;
        }

        if (ExactScalarOrder.TryEnclose(function, budget, out RationalEnclosure enclosure))
        {
            if (enclosure.Lower > BigRational.Zero)
            {
                sign = 1;
                return true;
            }

            if (enclosure.Upper < BigRational.Zero)
            {
                sign = -1;
                return true;
            }

            if (enclosure.IsExact && enclosure.Lower.IsZero)
            {
                sign = 0;
                return true;
            }
        }

        sign = default;
        return false;
    }

    private static void CollectAddend(ExactReal value, BigRational coefficient, IDictionary<string, ExactRealSubstitutionArithmeticLinearTerm> terms, ref BigRational rational, ref BigRational piCoefficient, ResourceBudget budget)
    {
        budget.Charge();
        switch (value)
        {
            case RationalReal constant:
                rational = Checked(rational + coefficient * constant.Value, budget);
                return;
            case AffinePiReal affine:
                piCoefficient = Checked(piCoefficient + coefficient * affine.PiCoefficient, budget);
                rational = Checked(rational + coefficient * affine.Constant, budget);
                return;
            case FunctionReal { Function: "add", Arguments: [var left, var right] }:
                CollectAddend(left, coefficient, terms, ref rational, ref piCoefficient, budget);
                CollectAddend(right, coefficient, terms, ref rational, ref piCoefficient, budget);
                return;
            case FunctionReal { Function: "negate", Arguments: [var operand] }:
                CollectAddend(operand, -coefficient, terms, ref rational, ref piCoefficient, budget);
                return;
            case FunctionReal { Function: "scale", Arguments: [var operand, RationalReal factor] }:
                CollectAddend(operand, Checked(coefficient * factor.Value, budget), terms, ref rational, ref piCoefficient, budget);
                return;
        }

        string canonical = ExactRealCanonical.Format(value);
        BigRational combined = coefficient;
        if (terms.TryGetValue(canonical, out ExactRealSubstitutionArithmeticLinearTerm? existing))
        {
            combined = Checked(existing.Coefficient + coefficient, budget);
        }

        terms[canonical] = new ExactRealSubstitutionArithmeticLinearTerm(value, combined);
    }

    private static void CollectFactor(ExactReal value, ref BigRational coefficient, ICollection<ExactReal> factors, ResourceBudget budget)
    {
        budget.Charge();
        switch (value)
        {
            case RationalReal rational:
                coefficient = Checked(coefficient * rational.Value, budget);
                return;
            case FunctionReal { Function: "multiply", Arguments: [var left, var right] }:
                CollectFactor(left, ref coefficient, factors, budget);
                CollectFactor(right, ref coefficient, factors, budget);
                return;
            case FunctionReal { Function: "scale", Arguments: [var operand, RationalReal factor] }:
                coefficient = Checked(coefficient * factor.Value, budget);
                CollectFactor(operand, ref coefficient, factors, budget);
                return;
            case FunctionReal { Function: "negate", Arguments: [var operand] }:
                coefficient = Checked(-coefficient, budget);
                CollectFactor(operand, ref coefficient, factors, budget);
                return;
            default:
                factors.Add(value);
                return;
        }
    }

    private static ExactReal ScalePrimitive(ExactReal value, BigRational factor, ResourceBudget budget)
    {
        if (factor.IsZero)
        {
            return new RationalReal(BigRational.Zero);
        }

        if (factor.IsOne)
        {
            return value;
        }

        return value switch
        {
            RationalReal rational => new RationalReal(Checked(rational.Value * factor, budget)),
            AffinePiReal affine => new AffinePiReal(Checked(affine.PiCoefficient * factor, budget), Checked(affine.Constant * factor, budget)),
            FunctionReal { Function: "scale", Arguments: [var operand, RationalReal existing] } => ScalePrimitive(operand, Checked(existing.Value * factor, budget), budget),
            FunctionReal { Function: "negate", Arguments: [var operand] } => ScalePrimitive(operand, Checked(-factor, budget), budget),
            _ when factor == BigRational.MinusOne => new FunctionReal("negate", [value]),
            _ => new FunctionReal("scale", [value, new RationalReal(factor)])
        };
    }

    private static FunctionReal Binary(string operation, ExactReal left, ExactReal right) => new(operation, ImmutableArray.Create(left, right));
    private static BigRational Checked(BigRational value, ResourceBudget budget)
    {
        budget.CheckCoefficient(value);
        return value;
    }

    private static void CheckPolynomial(UnivariatePolynomial polynomial, ResourceBudget budget)
    {
        foreach (BigRational coefficient in polynomial.Coefficients)
        {
            budget.CheckCoefficient(coefficient);
        }
    }

    private static void PreflightPower(BigRational basis, int exponent)
    {
        if (basis.IsZero || basis.Abs().IsOne || exponent == 0)
        {
            return;
        }

        long magnitude = Math.Abs((long)exponent);
        long guaranteedBits = Math.Max(checked((basis.Numerator.GetBitLength() - 1) * magnitude + 1), checked((basis.Denominator.GetBitLength() - 1) * magnitude + 1));
        if (guaranteedBits > AnalysisLimits.CoefficientBits)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.CoefficientBits));
        }
    }
}
