using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed class ExactOriginCertificateReplayReplayEvaluator
{
    private readonly string _variable;
    private readonly AngleUnit _angleUnit;
    private readonly ResourceBudget _budget;
    private readonly Dictionary<int, ExactReal> _values = [];
    private readonly HashSet<int> _failed = [];
    public ExactOriginCertificateReplayReplayEvaluator(string variable, AngleUnit angleUnit, ResourceBudget budget)
    {
        _variable = variable;
        _angleUnit = angleUnit;
        _budget = budget;
    }

    public bool TryTerm(ValueTerm term, out ExactReal value)
    {
        _budget.Charge();
        if (_values.TryGetValue(term.Id, out value!))
        {
            return true;
        }

        if (_failed.Contains(term.Id) || !TryTermCore(term, out value))
        {
            _failed.Add(term.Id);
            value = null!;
            return false;
        }

        _values.Add(term.Id, value);
        return true;
    }

    public bool TryFormula(Formula formula, out bool value)
    {
        _budget.Charge();
        switch (formula)
        {
            case BooleanFormula boolean:
                value = boolean.Value;
                return true;
            case ComparisonFormula comparison when TryTerm(comparison.Left, out ExactReal left) && TryTerm(comparison.Right, out ExactReal right):
                return ExactRealSubstitutionArithmetic.TryCompare(left, comparison.Comparison, right, _budget, out value);
            case PredicateFormula predicate:
                return TryPredicate(predicate, out value);
            case NotFormula not when TryFormula(not.Operand, out bool operand):
                value = !operand;
                return true;
            case JunctionFormula junction:
                return TryJunction(junction, out value);
            default:
                value = false;
                return false;
        }
    }

    private bool TryTermCore(ValueTerm term, out ExactReal value)
    {
        switch (term.Kind)
        {
            case ValueKind.Constant:
                _budget.CheckCoefficient(term.Constant);
                value = new RationalReal(term.Constant);
                return true;
            case ValueKind.Variable when term.Name.Equals(_variable, StringComparison.OrdinalIgnoreCase):
                value = new RationalReal(BigRational.Zero);
                return true;
            case ValueKind.SymbolicConstant:
                return TryNamed(term.Name, out value);
            case ValueKind.Negate when term.Operands is [var operand] && TryTerm(operand, out ExactReal negated):
                value = ExactRealSubstitutionArithmetic.Negate(negated, _budget);
                return true;
            case ValueKind.Add:
            case ValueKind.Subtract:
            case ValueKind.Multiply:
            case ValueKind.Divide:
                return TryBinary(term, out value);
            case ValueKind.Power:
                return TryPower(term, out value);
            case ValueKind.Function:
                return TryFunction(term, out value);
            default:
                value = null!;
                return false;
        }
    }

    private bool TryBinary(ValueTerm term, out ExactReal value)
    {
        if (term.Operands is not [var leftTerm, var rightTerm] || !TryTerm(leftTerm, out ExactReal left) || !TryTerm(rightTerm, out ExactReal right) || term.Kind == ValueKind.Divide && right is RationalReal { Value.IsZero: true })
        {
            value = null!;
            return false;
        }

        value = term.Kind switch
        {
            ValueKind.Add => ExactRealSubstitutionArithmetic.Add(left, right, _budget),
            ValueKind.Subtract => ExactRealSubstitutionArithmetic.Subtract(left, right, _budget),
            ValueKind.Multiply => ExactRealSubstitutionArithmetic.Multiply(left, right, _budget),
            ValueKind.Divide => ExactRealSubstitutionArithmetic.Divide(left, right, _budget),
            _ => throw new ArgumentOutOfRangeException(nameof(term))
        };
        return true;
    }

    private bool TryPower(ValueTerm term, out ExactReal value)
    {
        if (term.Operands is not [var basisTerm, var exponentTerm] || !TryTerm(basisTerm, out ExactReal basis) || !TryTerm(exponentTerm, out ExactReal exponentValue))
        {
            value = null!;
            return false;
        }

        if (exponentValue is RationalReal exponent)
        {
            if (basis is RationalReal { Value.IsZero: true } && exponent.Value.Sign <= 0)
            {
                value = null!;
                return false;
            }

            value = ExactRealSubstitutionArithmetic.Power(basis, exponent.Value, _budget);
            return true;
        }

        value = new FunctionReal("power", [basis, exponentValue]);
        return true;
    }

    private bool TryFunction(ValueTerm term, out ExactReal value)
    {
        if (term.Name is "min" or "max")
        {
            return TryExtremum(term, out value);
        }

        if (term.Name == "root")
        {
            return TryRoot(term, out value);
        }

        if (term.Operands is not [var argumentTerm] || !TryTerm(argumentTerm, out ExactReal argument))
        {
            value = null!;
            return false;
        }

        switch (term.Name)
        {
            case "abs":
                value = ExactRealSubstitutionArithmetic.Absolute(argument, _budget);
                return true;
            case "sqrt":
                value = ExactRealSubstitutionArithmetic.SquareRoot(argument, _budget);
                return true;
            case "sin":
            case "cos":
            case "tan":
                value = ForwardTrig(term.Name, argument);
                return true;
            case "asin":
            case "acos":
            case "atan":
                value = InverseTrig(term.Name, argument);
                return true;
            case "sinh":
            case "cosh":
            case "tanh":
                value = Hyperbolic(term.Name, argument);
                return true;
            case "exp":
                value = IsZero(argument) ? new RationalReal(BigRational.One) : IsOne(argument) ? new NamedReal("e") : new FunctionReal("exp", [argument]);
                return true;
            default:
                return TryRemainingFunction(term.Name, argument, out value);
        }
    }

    private bool TryExtremum(ValueTerm term, out ExactReal value)
    {
        if (term.Operands.IsEmpty)
        {
            value = null!;
            return false;
        }

        var arguments = ImmutableArray.CreateBuilder<ExactReal>(term.Operands.Length);
        foreach (ValueTerm operand in term.Operands)
        {
            if (!TryTerm(operand, out ExactReal argument))
            {
                value = null!;
                return false;
            }

            arguments.Add(argument);
        }

        ExactReal selected = arguments[0];
        Comparison comparison = term.Name == "min" ? Comparison.Less : Comparison.Greater;
        for (int index = 1; index < arguments.Count; index++)
        {
            ExactReal candidate = arguments[index];
            if (!ExactRealSubstitutionArithmetic.TryCompare(candidate, comparison, selected, _budget, out bool precedes))
            {
                value = new FunctionReal(term.Name, arguments.ToImmutable());
                return true;
            }

            if (precedes)
            {
                selected = candidate;
            }
        }

        value = selected;
        return true;
    }

    private bool TryRemainingFunction(string function, ExactReal argument, out ExactReal value)
    {
        if (function is "log" or "ln")
        {
            if (IsOne(argument))
            {
                value = new RationalReal(BigRational.Zero);
                return true;
            }

            if (function == "log" && argument is RationalReal { Value: var logarithm } && logarithm == new BigRational(10))
            {
                value = new RationalReal(BigRational.One);
                return true;
            }

            value = new FunctionReal(function, [argument]);
            return true;
        }

        if (function == "floor")
        {
            value = argument is RationalReal floor ? new RationalReal(new BigRational(floor.Value.Floor())) : new FunctionReal(function, [argument]);
            return true;
        }

        if (function == "ceil")
        {
            value = argument is RationalReal ceiling ? new RationalReal(new BigRational(ceiling.Value.Ceiling())) : new FunctionReal(function, [argument]);
            return true;
        }

        if (function == "sign")
        {
            value = ExactRealSubstitutionArithmetic.TrySign(argument, _budget, out int sign) ? new RationalReal(new BigRational(sign)) : new FunctionReal(function, [argument]);
            return true;
        }

        if (function == "factorial")
        {
            value = TryFactorial(argument, out BigRational factorial) ? new RationalReal(factorial) : new FunctionReal(function, [argument]);
            return true;
        }

        value = null!;
        return false;
    }

    private bool TryRoot(ValueTerm term, out ExactReal value)
    {
        if (term.Operands is not [var radicandTerm, var degreeTerm] || !TryTerm(radicandTerm, out ExactReal radicand) || !TryTerm(degreeTerm, out ExactReal degreeValue))
        {
            value = null!;
            return false;
        }

        if (degreeValue is RationalReal degree)
        {
            if (degree.Value.IsZero)
            {
                value = null!;
                return false;
            }

            value = ExactRealSubstitutionArithmetic.Root(radicand, degree.Value, _budget);
            return true;
        }

        value = new FunctionReal("root", [radicand, degreeValue]);
        return true;
    }

    private ExactReal ForwardTrig(string function, ExactReal argument)
    {
        ExactReal radians = ExactRealSubstitutionArithmetic.ValidateCoefficients(ExactAngleArithmetic.ToRadians(argument, _angleUnit), _budget);
        if (IsZero(radians))
        {
            return new RationalReal(function == "cos" ? BigRational.One : BigRational.Zero);
        }

        if (radians is AffinePiReal { Constant.IsZero: true, PiCoefficient: var coefficient } && TryQuarterTurnValue(function, coefficient, out ExactReal exact))
        {
            return exact;
        }

        return new FunctionReal(function, [radians]);
    }

    private static bool TryQuarterTurnValue(string function, BigRational piCoefficient, out ExactReal value)
    {
        BigRational quarterTurns = new BigRational(2) * piCoefficient;
        if (!quarterTurns.IsInteger)
        {
            value = null!;
            return false;
        }

        _ = ExactInteger.DivRem(quarterTurns.Numerator, new ExactInteger(4), out ExactInteger remainder);
        if (remainder.Sign < 0)
        {
            remainder += new ExactInteger(4);
        }

        int residue = (int)remainder;
        BigRational? exact = function switch
        {
            "sin" => residue switch
            {
                0 or 2 => BigRational.Zero,
                1 => BigRational.One,
                3 => BigRational.MinusOne,
                _ => null
            },
            "cos" => residue switch
            {
                0 => BigRational.One,
                1 or 3 => BigRational.Zero,
                2 => BigRational.MinusOne,
                _ => null
            },
            "tan" when residue is 0 or 2 => BigRational.Zero,
            _ => null
        };
        value = exact is { } rational ? new RationalReal(rational) : null!;
        return exact is not null;
    }

    private ExactReal InverseTrig(string function, ExactReal argument) => ExactRealSubstitutionArithmetic.ValidateCoefficients(argument is RationalReal rational ? ExactInverseTrigonometry.PrincipalAngle(function, ExactScalar.FromRational(rational.Value), _angleUnit) : ExactAngleArithmetic.FromRadians(new FunctionReal(function, [argument]), _angleUnit), _budget);
    private static ExactReal Hyperbolic(string function, ExactReal argument)
    {
        if (IsZero(argument))
        {
            return new RationalReal(function == "cosh" ? BigRational.One : BigRational.Zero);
        }

        return new FunctionReal(function, [argument]);
    }

    private bool TryPredicate(PredicateFormula predicate, out bool value)
    {
        if (predicate is { Kind: ExactPredicate.FunctionIsDefined, Terms: [{ Kind: ValueKind.Function, Name: "min" or "max", Operands.IsEmpty: false }] })
        {
            value = true;
            return true;
        }

        if (predicate.Terms is not [var term] || !TryTerm(term, out ExactReal evaluated) || evaluated is not RationalReal rational)
        {
            value = false;
            return false;
        }

        value = predicate.Kind switch
        {
            ExactPredicate.IsInteger => rational.Value.IsInteger,
            ExactPredicate.IsOddInteger => rational.Value.IsInteger && !rational.Value.Numerator.IsEven,
            _ => false
        };
        return predicate.Kind is ExactPredicate.IsInteger or ExactPredicate.IsOddInteger;
    }

    private bool TryJunction(JunctionFormula junction, out bool value)
    {
        bool unknown = false;
        foreach (Formula operand in junction.Operands)
        {
            if (!TryFormula(operand, out bool operandValue))
            {
                unknown = true;
                continue;
            }

            if (junction.IsConjunction && !operandValue)
            {
                value = false;
                return true;
            }

            if (!junction.IsConjunction && operandValue)
            {
                value = true;
                return true;
            }
        }

        if (unknown)
        {
            value = false;
            return false;
        }

        value = junction.IsConjunction;
        return true;
    }

    private bool TryFactorial(ExactReal argument, out BigRational factorial)
    {
        if (argument is not RationalReal { Value.IsInteger: true, Value.Sign: >= 0 } rational || rational.Value.Numerator > int.MaxValue)
        {
            factorial = default;
            return false;
        }

        int limit = (int)rational.Value.Numerator;
        factorial = BigRational.One;
        for (int factor = 2; factor <= limit; factor++)
        {
            _budget.Charge();
            factorial *= new BigRational(factor);
            _budget.CheckCoefficient(factorial);
        }

        return true;
    }

    private static bool TryNamed(string name, out ExactReal value)
    {
        switch (name)
        {
            case "pi":
                value = new AffinePiReal(BigRational.One, BigRational.Zero);
                return true;
            case "e":
                value = new NamedReal("e");
                return true;
            default:
                value = null!;
                return false;
        }
    }

    private static bool IsZero(ExactReal value) => value switch
    {
        RationalReal rational => rational.Value.IsZero,
        AffinePiReal affine => affine.PiCoefficient.IsZero && affine.Constant.IsZero,
        _ => false
    };
    private static bool IsOne(ExactReal value) => value is RationalReal { Value.IsOne: true };
}
