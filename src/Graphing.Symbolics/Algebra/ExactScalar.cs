namespace Graphing.Symbolics;
/// <summary>
/// An exact, variable-independent real value whose sign has been proved by
/// structural rules. This is deliberately separate from polynomial
/// coefficients: Sturm, gcd, and CAD arithmetic continue to operate over
/// <see cref = "BigRational"/> only.
/// </summary>
internal readonly record struct ExactScalar
{
    private ExactScalar(ExactReal value, int sign, BigRational? rationalValue)
    {
        if (sign is < -1 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sign));
        }

        Value = value;
        Sign = sign;
        RationalValue = rationalValue;
    }

    public ExactReal Value { get; }
    public int Sign { get; }
    public BigRational? RationalValue { get; }
    public bool IsZero => Sign == 0;
    public bool IsOne => RationalValue is { IsOne: true };
    public string Canonical => $"{ExactRealCanonical.Format(Value)}:sign={Sign}";
    public static ExactScalar Zero { get; } = FromRational(BigRational.Zero);
    public static ExactScalar One { get; } = FromRational(BigRational.One);

    public static ExactScalar FromRational(BigRational value) => new(new RationalReal(value), value.Sign, value);
    public ExactScalar Negate() => IsZero ? this : new ExactScalar(ExactRealArithmetic.Negate(Value), -Sign, RationalValue is { } rational ? -rational : null);
    public ExactScalar Abs() => Sign < 0 ? Negate() : this;
    public ExactScalar Multiply(ExactScalar other, ResourceBudget budget)
    {
        if (IsZero || other.IsZero)
        {
            return Zero;
        }

        if (IsOne)
        {
            return other;
        }

        if (other.IsOne)
        {
            return this;
        }

        BigRational? rational = RationalValue is { } left && other.RationalValue is { } right ? left * right : null;
        if (rational is { } exact)
        {
            budget.CheckCoefficient(exact);
        }

        return new ExactScalar(Checked(ExactRealArithmetic.Multiply(Value, other.Value), budget), Sign * other.Sign, rational);
    }

    public ExactScalar Reciprocal(ResourceBudget budget)
    {
        if (IsZero)
        {
            throw new DivideByZeroException();
        }

        BigRational? rational = RationalValue is { } value ? value.Reciprocal() : null;
        if (rational is { } exact)
        {
            budget.CheckCoefficient(exact);
        }

        return new ExactScalar(ExactRealArithmetic.Divide(new RationalReal(BigRational.One), Value), Sign, rational);
    }

    public bool TrySquareRoot(ResourceBudget budget, out ExactScalar result)
    {
        budget.Charge();
        if (Sign < 0)
        {
            result = default;
            return false;
        }

        if (RationalValue is { } rational && BigRational.TrySquareRoot(rational, out BigRational exact))
        {
            result = FromRational(exact);
            return true;
        }

        result = IsZero ? Zero : new ExactScalar(new FunctionReal("sqrt", [Value]), 1, null);
        return true;
    }

    public bool TryCompareAbsoluteTo(BigRational other, ResourceBudget budget, out int comparison) => ExactScalarOrder.TryCompareAbsolute(this, other.Abs(), budget, out comparison);
    public static bool TryCreate(ValueTerm term, ResourceBudget budget, out ExactScalar scalar)
    {
        budget.Charge();
        if (term.Kind == ValueKind.Constant)
        {
            budget.CheckCoefficient(term.Constant);
            scalar = FromRational(term.Constant);
            return true;
        }

        if (term.Kind == ValueKind.SymbolicConstant)
        {
            return TryNamedConstant(term.Name, out scalar);
        }

        if (term.Kind == ValueKind.Negate)
        {
            bool recognized = TryCreate(term.Operands[0], budget, out ExactScalar negated);
            scalar = recognized ? negated.Negate() : default;
            return recognized;
        }

        return term.Kind switch
        {
            ValueKind.Add or ValueKind.Subtract or ValueKind.Multiply or ValueKind.Divide => TryCreateBinary(term, budget, out scalar),
            ValueKind.Power => TryCreatePower(term, budget, out scalar),
            ValueKind.Function => TryCreateFunction(term, budget, out scalar),
            _ => Fail(out scalar)
        };
    }

    private static bool TryNamedConstant(string name, out ExactScalar scalar)
    {
        switch (name)
        {
            case "pi":
                scalar = new ExactScalar(new AffinePiReal(BigRational.One, BigRational.Zero), 1, null);
                return true;
            case "e":
                scalar = new ExactScalar(new NamedReal("e"), 1, null);
                return true;
            default:
                return Fail(out scalar);
        }
    }

    private static bool TryCreateBinary(ValueTerm term, ResourceBudget budget, out ExactScalar scalar)
    {
        if (!TryCreate(term.Operands[0], budget, out ExactScalar left) || !TryCreate(term.Operands[1], budget, out ExactScalar right))
        {
            return Fail(out scalar);
        }

        switch (term.Kind)
        {
            case ValueKind.Add:
                return TryAdd(left, right, budget, out scalar);
            case ValueKind.Subtract:
                return TryAdd(left, right.Negate(), budget, out scalar);
            case ValueKind.Multiply:
                scalar = left.Multiply(right, budget);
                return true;
            case ValueKind.Divide when !right.IsZero:
                scalar = left.Multiply(right.Reciprocal(budget), budget);
                return true;
            default:
                return Fail(out scalar);
        }
    }

    private static bool TryCreatePower(ValueTerm term, ResourceBudget budget, out ExactScalar scalar)
    {
        if (term.Operands[1].Kind != ValueKind.Constant || !term.Operands[1].Constant.IsInteger || term.Operands[1].Constant.Numerator < int.MinValue || term.Operands[1].Constant.Numerator > int.MaxValue || !TryCreate(term.Operands[0], budget, out ExactScalar basis))
        {
            return Fail(out scalar);
        }

        return TryPower(basis, (int)term.Operands[1].Constant.Numerator, budget, out scalar);
    }

    private static bool TryCreateFunction(ValueTerm term, ResourceBudget budget, out ExactScalar scalar) => term.Name switch
    {
        "sqrt" => TryCreateSquareRoot(term, budget, out scalar),
        "abs" => TryCreateAbsolute(term, budget, out scalar),
        "exp" => TryCreateExponential(term, budget, out scalar),
        "root" => TryCreateRoot(term, budget, out scalar),
        _ => Fail(out scalar)
    };
    private static bool TryCreateSquareRoot(ValueTerm term, ResourceBudget budget, out ExactScalar scalar)
    {
        if (term.Operands.Length != 1 || !TryCreate(term.Operands[0], budget, out ExactScalar radicand) || radicand.Sign < 0)
        {
            return Fail(out scalar);
        }

        if (radicand.RationalValue is { } rational && BigRational.TrySquareRoot(rational, out BigRational root))
        {
            scalar = FromRational(root);
            return true;
        }

        scalar = radicand.IsZero ? Zero : new ExactScalar(new FunctionReal("sqrt", [radicand.Value]), 1, null);
        return true;
    }

    private static bool TryCreateAbsolute(ValueTerm term, ResourceBudget budget, out ExactScalar scalar)
    {
        if (term.Operands.Length != 1 || !TryCreate(term.Operands[0], budget, out ExactScalar value))
        {
            return Fail(out scalar);
        }

        scalar = value.Abs();
        return true;
    }

    private static bool TryCreateExponential(ValueTerm term, ResourceBudget budget, out ExactScalar scalar)
    {
        if (term.Operands.Length != 1 || !TryCreate(term.Operands[0], budget, out ExactScalar exponent))
        {
            return Fail(out scalar);
        }

        scalar = exponent.IsZero ? One : new ExactScalar(new FunctionReal("exp", [exponent.Value]), 1, null);
        return true;
    }

    private static bool TryCreateRoot(ValueTerm term, ResourceBudget budget, out ExactScalar scalar)
    {
        if (term.Operands.Length != 2 || !TryCreate(term.Operands[0], budget, out ExactScalar radicand) || term.Operands[1].Kind != ValueKind.Constant || !term.Operands[1].Constant.IsInteger || term.Operands[1].Constant.IsZero)
        {
            return Fail(out scalar);
        }

        BigRational degree = term.Operands[1].Constant;
        bool defined = radicand.Sign > 0 || (radicand.IsZero && degree.Sign > 0) || (radicand.Sign < 0 && !degree.Numerator.IsEven);
        if (!defined)
        {
            return Fail(out scalar);
        }

        scalar = radicand.IsZero ? Zero : new ExactScalar(new FunctionReal("root", [radicand.Value, new RationalReal(degree)]), radicand.Sign, null);
        return true;
    }

    private static bool Fail(out ExactScalar scalar)
    {
        scalar = default;
        return false;
    }

    private static ExactReal Checked(ExactReal value, ResourceBudget budget)
    {
        switch (value)
        {
            case RationalReal rational:
                budget.CheckCoefficient(rational.Value);
                break;
            case AffinePiReal affine:
                budget.CheckCoefficient(affine.PiCoefficient);
                budget.CheckCoefficient(affine.Constant);
                break;
        }

        return value;
    }

    public static bool TryAdd(ExactScalar left, ExactScalar right, ResourceBudget budget, out ExactScalar result)
    {
        if (left.RationalValue is { } leftRational && right.RationalValue is { } rightRational)
        {
            BigRational sum = leftRational + rightRational;
            budget.CheckCoefficient(sum);
            result = FromRational(sum);
            return true;
        }

        if (left.IsZero)
        {
            result = right;
            return true;
        }

        if (right.IsZero)
        {
            result = left;
            return true;
        }

        if (string.Equals(ExactRealCanonical.Format(left.Value), ExactRealCanonical.Format(ExactRealArithmetic.Negate(right.Value)), StringComparison.Ordinal))
        {
            result = Zero;
            return true;
        }

        if (left.Sign != right.Sign)
        {
            if (TryProvePiAndEulerDifference(left, right, budget, out result))
            {
                return true;
            }

            ExactReal sum = Checked(ExactRealArithmetic.Add(left.Value, right.Value), budget);
            if (ExactScalarOrder.TryEnclose(sum, budget, out RationalEnclosure enclosure))
            {
                int sign = enclosure.Lower > BigRational.Zero ? 1 : enclosure.Upper < BigRational.Zero ? -1 : enclosure.IsExact && enclosure.Lower.IsZero ? 0 : 2;
                if (sign is >= -1 and <= 1)
                {
                    result = sign == 0 ? Zero : new ExactScalar(sum, sign, null);
                    return true;
                }
            }

            result = default;
            return false;
        }

        result = new ExactScalar(Checked(ExactRealArithmetic.Add(left.Value, right.Value), budget), left.Sign, null);
        return true;
    }

    private static bool TryProvePiAndEulerDifference(ExactScalar left, ExactScalar right, ResourceBudget budget, out ExactScalar result)
    {
        // Archimedes' lower bound pi > 3 and the factorial-series upper
        // bound e < 3 prove pi - e > 0 without a floating-point comparison.
        // The reflected and commuted forms follow by exact negation.
        bool positive = IsPi(left.Value) && IsNegativeEuler(right.Value) || IsNegativeEuler(left.Value) && IsPi(right.Value);
        bool negative = IsEuler(left.Value) && IsNegativePi(right.Value) || IsNegativePi(left.Value) && IsEuler(right.Value);
        if (!positive && !negative)
        {
            result = default;
            return false;
        }

        result = new ExactScalar(Checked(ExactRealArithmetic.Add(left.Value, right.Value), budget), positive ? 1 : -1, null);
        return true;
    }

    private static bool IsPi(ExactReal value) => value is AffinePiReal { PiCoefficient.IsOne: true, Constant.IsZero: true };
    private static bool IsNegativePi(ExactReal value) => value is AffinePiReal { PiCoefficient: var coefficient, Constant.IsZero: true } && coefficient == BigRational.MinusOne;
    private static bool IsEuler(ExactReal value) => value is NamedReal { Name: "e" };
    private static bool IsNegativeEuler(ExactReal value) => value is FunctionReal { Function: "negate", Arguments: [NamedReal { Name: "e" }] };
    private static bool TryPower(ExactScalar basis, int exponent, ResourceBudget budget, out ExactScalar result)
    {
        if (exponent <= 0 && basis.IsZero)
        {
            result = default;
            return false;
        }

        if (exponent == 0)
        {
            result = One;
            return true;
        }

        if (basis.RationalValue is { } rational)
        {
            PreflightRationalPower(rational, exponent);
            BigRational power = rational.Pow(exponent);
            budget.CheckCoefficient(power);
            result = FromRational(power);
            return true;
        }

        int sign = (exponent & 1) == 0 ? 1 : basis.Sign;
        result = new ExactScalar(ExactRealArithmetic.Power(basis.Value, exponent), sign, null);
        return true;
    }

    private static void PreflightRationalPower(BigRational value, int exponent)
    {
        if (value.IsZero || value.Abs().IsOne || exponent == 0)
        {
            return;
        }

        long magnitude = Math.Abs((long)exponent);
        long guaranteedBits = Math.Max(checked((value.Numerator.GetBitLength() - 1) * magnitude + 1), checked((value.Denominator.GetBitLength() - 1) * magnitude + 1));
        if (guaranteedBits > AnalysisLimits.CoefficientBits)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.CoefficientBits));
        }
    }
}
