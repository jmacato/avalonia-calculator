using System.Collections.Immutable;
using System.Globalization;

namespace Graphing.Symbolics;

internal sealed class SemanticGraphBuilder
{
    private readonly ResourceBudget _budget;
    private readonly Dictionary<string, ValueTerm> _terms = new(StringComparer.Ordinal);
    private readonly Dictionary<InputExpression, SemanticExpression> _expressions = [];
    private int _nextTermId;
    public SemanticGraphBuilder(ResourceBudget budget)
    {
        _budget = budget;
    }

    public int InternedTermCount => _terms.Count;

    public SemanticExpression Build(InputExpression input)
    {
        if (_expressions.TryGetValue(input, out SemanticExpression? existing))
        {
            return existing;
        }

        _budget.AddNode();
        ImmutableArray<SemanticExpression> operands = input.Arguments.Select(Build).ToImmutableArray();
        SemanticExpression expression = input.Kind switch
        {
            InputExpressionKind.Constant => Leaf(Constant(input.Constant), input.Source),
            InputExpressionKind.Variable => Leaf(VariableOrConstant(input.Name), input.Source),
            InputExpressionKind.Negate => Unary(ValueKind.Negate, operands[0], input.Source),
            InputExpressionKind.Add => Binary(ValueKind.Add, operands[0], operands[1], input.Source),
            InputExpressionKind.Subtract => Binary(ValueKind.Subtract, operands[0], operands[1], input.Source),
            InputExpressionKind.Multiply => Multiply(operands[0], operands[1], input.Source),
            InputExpressionKind.Divide => Divide(operands[0], operands[1], input.Source),
            InputExpressionKind.Power => Power(operands[0], operands[1], input.Source),
            InputExpressionKind.Function => Function(input.Name, operands, input.Source),
            _ => throw new ArgumentOutOfRangeException(nameof(input))
        };
        _expressions.Add(input, expression);
        return expression;
    }

    private static SemanticExpression Leaf(ValueTerm value, SourceRange source) => new(value, Formula.True, Formula.True, Formula.True, [source], [], []);
    private SemanticExpression Unary(ValueKind kind, SemanticExpression operand, SourceRange source)
    {
        RewriteStep? rewrite = null;
        ValueTerm value;
        if (kind == ValueKind.Negate && operand.Value.Kind == ValueKind.Constant)
        {
            value = Constant(-operand.Value.Constant);
            rewrite = new RewriteStep("exact-unary-constant-fold", TermCanonical(kind, default, string.Empty, [operand.Value]), value.Canonical, Formula.True);
        }
        else
        {
            value = Term(kind, default, string.Empty, [operand.Value]);
        }

        return new SemanticExpression(value, operand.DefinedWhen, operand.ContinuousWhen, operand.DifferentiableWhen, MergeProvenance(source, [operand]), rewrite is null ? [] : [rewrite.Value], [operand]);
    }

    private SemanticExpression Binary(ValueKind kind, SemanticExpression left, SemanticExpression right, SourceRange source)
    {
        ValueTerm value = FoldBinary(kind, left.Value, right.Value, out RewriteStep? rewrite);
        return new SemanticExpression(value, Formula.And(left.DefinedWhen, right.DefinedWhen), Formula.And(left.ContinuousWhen, right.ContinuousWhen), Formula.And(left.DifferentiableWhen, right.DifferentiableWhen), MergeProvenance(source, [left, right]), rewrite is null ? [] : [rewrite.Value], [left, right]);
    }

    private SemanticExpression Multiply(SemanticExpression left, SemanticExpression right, SourceRange source)
    {
        ValueTerm value = FoldBinary(ValueKind.Multiply, left.Value, right.Value, out RewriteStep? rewrite);
        return new SemanticExpression(value, Formula.And(left.DefinedWhen, right.DefinedWhen), Formula.And(left.ContinuousWhen, right.ContinuousWhen), Formula.And(left.DifferentiableWhen, right.DifferentiableWhen), MergeProvenance(source, [left, right]), rewrite is null ? [] : [rewrite.Value], [left, right]);
    }

    private SemanticExpression Divide(SemanticExpression numerator, SemanticExpression denominator, SourceRange source)
    {
        Formula nonzero = ExactScalar.TryCreate(denominator.Value, _budget, out ExactScalar scalarDenominator) ? scalarDenominator.IsZero ? Formula.False : Formula.True : Formula.Compare(denominator.Value, Comparison.NotEqual, Constant(BigRational.Zero));
        Formula defined = Formula.And(numerator.DefinedWhen, denominator.DefinedWhen, nonzero);
        ValueTerm value;
        RewriteStep? rewrite;
        if (TryRewriteGuardedTangentRatio(numerator.Value, denominator.Value, nonzero, out ValueTerm tangent, out RewriteStep tangentRewrite))
        {
            value = tangent;
            rewrite = tangentRewrite;
        }
        else
        {
            value = FoldBinary(ValueKind.Divide, numerator.Value, denominator.Value, out rewrite);
        }

        return new SemanticExpression(value, defined, Formula.And(numerator.ContinuousWhen, denominator.ContinuousWhen, nonzero), Formula.And(numerator.DifferentiableWhen, denominator.DifferentiableWhen, nonzero), MergeProvenance(source, [numerator, denominator]), rewrite is null ? [] : [rewrite.Value], [numerator, denominator]);
    }

    private bool TryRewriteGuardedTangentRatio(ValueTerm numerator, ValueTerm denominator, Formula nonzero, out ValueTerm value, out RewriteStep rewrite)
    {
        if (numerator is not { Kind: ValueKind.Function, Name: "sin", Operands: [var numeratorArgument] } || denominator is not { Kind: ValueKind.Function, Name: "cos", Operands: [var denominatorArgument] } || numeratorArgument.Id != denominatorArgument.Id)
        {
            value = null!;
            rewrite = default;
            return false;
        }

        value = Term(ValueKind.Function, default, "tan", [numeratorArgument]);
        rewrite = new RewriteStep("guarded-sine-cosine-to-tangent", TermCanonical(ValueKind.Divide, default, string.Empty, [numerator, denominator]), value.Canonical, nonzero);
        return true;
    }

    private SemanticExpression Power(SemanticExpression basis, SemanticExpression exponent, SourceRange source)
    {
        ValueTerm zero = Constant(BigRational.Zero);
        Formula defined;
        if (exponent.Value.Kind == ValueKind.Constant)
        {
            BigRational power = exponent.Value.Constant;
            if (power.IsInteger)
            {
                defined = power.Sign > 0 ? Formula.And(basis.DefinedWhen, exponent.DefinedWhen) : Formula.And(basis.DefinedWhen, exponent.DefinedWhen, Formula.Compare(basis.Value, Comparison.NotEqual, zero));
            }
            else
            {
                Comparison comparison = power.Sign > 0 ? Comparison.GreaterOrEqual : Comparison.Greater;
                defined = Formula.And(basis.DefinedWhen, exponent.DefinedWhen, Formula.Compare(basis.Value, comparison, zero));
            }
        }
        else
        {
            Formula positiveBase = Formula.Compare(basis.Value, Comparison.Greater, zero);
            Formula positiveZero = Formula.And(Formula.Compare(basis.Value, Comparison.Equal, zero), Formula.Compare(exponent.Value, Comparison.Greater, zero));
            Formula integralNegativeBase = Formula.And(Formula.Compare(basis.Value, Comparison.Less, zero), Formula.Predicate(ExactPredicate.IsInteger, exponent.Value));
            defined = Formula.And(basis.DefinedWhen, exponent.DefinedWhen, Formula.Or(positiveBase, positiveZero, integralNegativeBase));
        }

        ValueTerm value = Term(ValueKind.Power, default, string.Empty, [basis.Value, exponent.Value]);
        return new SemanticExpression(value, defined, Formula.And(defined, Formula.Predicate(ExactPredicate.PowerIsContinuous, basis.Value, exponent.Value)), Formula.And(defined, Formula.Predicate(ExactPredicate.PowerIsDifferentiable, basis.Value, exponent.Value)), MergeProvenance(source, [basis, exponent]), [], [basis, exponent]);
    }

    private SemanticExpression Function(string name, ImmutableArray<SemanticExpression> operands, SourceRange source)
    {
        string function = NormalizeAsciiIdentifier(name) switch
        {
            "arcsin" => "asin",
            "arccos" => "acos",
            "arctan" => "atan",
            "log10" => "log",
            "pow" => "power",
            var canonical => canonical
        };
        if (TryBuildOperatorFunction(function, operands, source, out SemanticExpression lowered))
        {
            return lowered;
        }

        if (TryBuildSpecialFunction(function, operands, source, out SemanticExpression special))
        {
            return special;
        }

        if (operands.Length == 1 && TryGuardedInverseComposition(function, operands[0], source, out SemanticExpression? inverse))
        {
            return inverse;
        }

        ValueTerm value = Term(ValueKind.Function, default, function, operands.Select(static operand => operand.Value).ToImmutableArray());
        Formula childrenDefined = Formula.And(operands.Select(static operand => operand.DefinedWhen));
        Formula childrenContinuous = Formula.And(operands.Select(static operand => operand.ContinuousWhen));
        Formula childrenDifferentiable = Formula.And(operands.Select(static operand => operand.DifferentiableWhen));
        ValueTerm zero = Constant(BigRational.Zero);
        ValueTerm one = Constant(BigRational.One);
        ValueTerm minusOne = Constant(BigRational.MinusOne);
        Formula localDefined;
        Formula localContinuous;
        Formula localDifferentiable;
        switch (function)
        {
            case "sin":
            case "cos":
            case "atan":
            case "sinh":
            case "cosh":
            case "tanh":
            case "exp":
                localDefined = Formula.True;
                localContinuous = Formula.True;
                localDifferentiable = Formula.True;
                break;
            case "tan":
                ValueTerm cosine = Term(ValueKind.Function, default, "cos", [operands[0].Value]);
                localDefined = Formula.Compare(cosine, Comparison.NotEqual, zero);
                localContinuous = localDefined;
                localDifferentiable = localDefined;
                break;
            case "log":
            case "ln":
                localDefined = Formula.Compare(operands[0].Value, Comparison.Greater, zero);
                localContinuous = localDefined;
                localDifferentiable = localDefined;
                break;
            case "asin":
            case "acos":
                localDefined = Formula.And(Formula.Compare(operands[0].Value, Comparison.GreaterOrEqual, minusOne), Formula.Compare(operands[0].Value, Comparison.LessOrEqual, one));
                localContinuous = localDefined;
                localDifferentiable = Formula.And(Formula.Compare(operands[0].Value, Comparison.Greater, minusOne), Formula.Compare(operands[0].Value, Comparison.Less, one));
                break;
            case "abs":
                localDefined = Formula.True;
                localContinuous = Formula.True;
                ValueTerm derivative = Term(ValueKind.Function, default, "derivative", [operands[0].Value]);
                localDifferentiable = Formula.Or(Formula.Compare(operands[0].Value, Comparison.NotEqual, zero), Formula.Compare(derivative, Comparison.Equal, zero));
                break;
            case "factorial":
                localDefined = Formula.And(Formula.Predicate(ExactPredicate.IsInteger, operands[0].Value), Formula.Compare(operands[0].Value, Comparison.GreaterOrEqual, zero));
                localContinuous = localDefined;
                localDifferentiable = localDefined;
                break;
            case "floor":
            case "ceil":
            case "sign":
                localDefined = Formula.True;
                localContinuous = Formula.Predicate(ExactPredicate.IsLocallyConstant, value);
                localDifferentiable = localContinuous;
                break;
            default:
                localDefined = Formula.Predicate(ExactPredicate.FunctionIsDefined, value);
                localContinuous = Formula.Predicate(ExactPredicate.FunctionIsContinuous, value);
                localDifferentiable = Formula.Predicate(ExactPredicate.FunctionIsDifferentiable, value);
                break;
        }

        return new SemanticExpression(value, Formula.And(childrenDefined, localDefined), Formula.And(childrenContinuous, localContinuous), Formula.And(childrenDifferentiable, localDifferentiable), MergeProvenance(source, operands), [], operands);
    }

    private bool TryBuildOperatorFunction(string function, ImmutableArray<SemanticExpression> operands, SourceRange source, out SemanticExpression expression)
    {
        switch (function, operands.Length)
        {
            case ("subtract", 2):
                expression = Binary(ValueKind.Subtract, operands[0], operands[1], source);
                return true;
            case ("divide", 2):
                expression = Divide(operands[0], operands[1], source);
                return true;
            case ("power", 2):
                expression = Power(operands[0], operands[1], source);
                return true;
            case ("root", 1):
                expression = SquareRoot(operands[0], source);
                return true;
            case ("sum", > 0):
                expression = AggregateOperator(operands, source, ValueKind.Add);
                return true;
            case ("product", > 0):
                expression = AggregateOperator(operands, source, ValueKind.Multiply);
                return true;
            case ("min" or "max", 1):
                expression = IdentityFunction(operands[0], source);
                return true;
            default:
                expression = null!;
                return false;
        }
    }

    private SemanticExpression AggregateOperator(ImmutableArray<SemanticExpression> operands, SourceRange source, ValueKind operation)
    {
        var level = operands.Select(operand => IdentityFunction(operand, source)).ToList();
        while (level.Count > 1)
        {
            var next = new List<SemanticExpression>((level.Count + 1) / 2);
            for (int index = 0; index < level.Count; index += 2)
            {
                if (index + 1 == level.Count)
                {
                    next.Add(level[index]);
                    continue;
                }

                next.Add(operation == ValueKind.Add ? Binary(ValueKind.Add, level[index], level[index + 1], source) : Multiply(level[index], level[index + 1], source));
            }

            level = next;
        }

        return level[0];
    }

    private static SemanticExpression IdentityFunction(SemanticExpression operand, SourceRange source) => new(operand.Value, operand.DefinedWhen, operand.ContinuousWhen, operand.DifferentiableWhen, MergeProvenance(source, [operand]), [], [operand]);
    private bool TryBuildSpecialFunction(string function, ImmutableArray<SemanticExpression> operands, SourceRange source, out SemanticExpression expression)
    {
        expression = (function, operands.Length) switch
        {
            ("sqrt", 1) => SquareRoot(operands[0], source),
            ("root", 2) => Root(operands[0], operands[1], source),
            ("cot" or "sec" or "csc", 1) => LowerReciprocalTrigonometric(function, operands[0], source),
            _ => null!
        };
        return expression is not null;
    }

    private SemanticExpression LowerReciprocalTrigonometric(string function, SemanticExpression operand, SourceRange source)
    {
        ValueTerm sine = Term(ValueKind.Function, default, "sin", [operand.Value]);
        ValueTerm cosine = Term(ValueKind.Function, default, "cos", [operand.Value]);
        ValueTerm numerator = function == "cot" ? cosine : Constant(BigRational.One);
        ValueTerm denominator = function == "sec" ? cosine : sine;
        ValueTerm zero = Constant(BigRational.Zero);
        Formula denominatorNonzero = Formula.Compare(denominator, Comparison.NotEqual, zero);
        Formula defined = Formula.And(operand.DefinedWhen, denominatorNonzero);
        ValueTerm value = FoldBinary(ValueKind.Divide, numerator, denominator, out _);
        ValueTerm before = Term(ValueKind.Function, default, function, [operand.Value]);
        RewriteStep rewrite = new(function switch
        {
            "cot" => "cotangent-to-guarded-cosine-sine-ratio",
            "sec" => "secant-to-guarded-cosine-reciprocal",
            "csc" => "cosecant-to-guarded-sine-reciprocal",
            _ => throw new ArgumentOutOfRangeException(nameof(function))
        }, before.Canonical, value.Canonical, defined);
        return new SemanticExpression(value, defined, Formula.And(operand.ContinuousWhen, denominatorNonzero), Formula.And(operand.DifferentiableWhen, denominatorNonzero), MergeProvenance(source, [operand]), [rewrite], [operand]);
    }

    private bool TryGuardedInverseComposition(string outerFunction, SemanticExpression inner, SourceRange source, out SemanticExpression expression)
    {
        if (inner.Value is not { Kind: ValueKind.Function, Operands.Length: 1 } innerValue || inner.SourceOperands.Length != 1)
        {
            expression = null!;
            return false;
        }

        string? rule = (outerFunction, innerValue.Name) switch
        {
            ("sin", "asin") => "sine-arcsine-identity",
            ("cos", "acos") => "cosine-arccosine-identity",
            ("tan", "atan") => "tangent-arctangent-identity",
            ("exp", "ln") => "exponential-logarithm-identity",
            ("ln", "exp") => "logarithm-exponential-identity",
            _ => null
        };
        if (rule is null)
        {
            expression = null!;
            return false;
        }

        SemanticExpression argument = inner.SourceOperands[0];
        Formula defined = inner.DefinedWhen;
        ValueTerm before = Term(ValueKind.Function, default, outerFunction, [inner.Value]);
        RewriteStep rewrite = new(rule, before.Canonical, argument.Value.Canonical, defined);
        expression = new SemanticExpression(argument.Value, defined, inner.ContinuousWhen, inner.DifferentiableWhen, MergeProvenance(source, [inner]), [rewrite], [inner]);
        return true;
    }

    private SemanticExpression SquareRoot(SemanticExpression operand, SourceRange source)
    {
        ValueTerm zero = Constant(BigRational.Zero);
        if (operand.Value is { Kind: ValueKind.Power, Operands.Length: 2 } power && power.Operands[1].Kind == ValueKind.Constant && power.Operands[1].Constant == new BigRational(2))
        {
            ValueTerm absolute = Term(ValueKind.Function, default, "abs", [power.Operands[0]]);
            RewriteStep rewrite = new("sqrt-square-to-absolute-value", $"sqrt({operand.Value.Canonical})", absolute.Canonical, Formula.True);
            ValueTerm derivative = Term(ValueKind.Function, default, "derivative", [power.Operands[0]]);
            Formula differentiable = Formula.And(operand.DifferentiableWhen, Formula.Or(Formula.Compare(power.Operands[0], Comparison.NotEqual, zero), Formula.Compare(derivative, Comparison.Equal, zero)));
            return new SemanticExpression(absolute, operand.DefinedWhen, operand.ContinuousWhen, differentiable, MergeProvenance(source, [operand]), [rewrite], [operand]);
        }

        ValueTerm value = Term(ValueKind.Function, default, "sqrt", [operand.Value]);
        Formula nonnegative = Formula.Compare(operand.Value, Comparison.GreaterOrEqual, zero);
        Formula positive = Formula.Compare(operand.Value, Comparison.Greater, zero);
        return new SemanticExpression(value, Formula.And(operand.DefinedWhen, nonnegative), Formula.And(operand.ContinuousWhen, nonnegative), Formula.And(operand.DifferentiableWhen, positive), MergeProvenance(source, [operand]), [], [operand]);
    }

    private SemanticExpression Root(SemanticExpression radicand, SemanticExpression degree, SourceRange source)
    {
        ValueTerm value = Term(ValueKind.Function, default, "root", [radicand.Value, degree.Value]);
        ValueTerm zero = Constant(BigRational.Zero);
        Formula degreeNonzero = Formula.Compare(degree.Value, Comparison.NotEqual, zero);
        Formula positiveRadicand = Formula.Compare(radicand.Value, Comparison.Greater, zero);
        Formula zeroRadicand = Formula.And(Formula.Compare(radicand.Value, Comparison.Equal, zero), Formula.Compare(degree.Value, Comparison.Greater, zero));
        Formula negativeRadicand = Formula.And(Formula.Compare(radicand.Value, Comparison.Less, zero), Formula.Predicate(ExactPredicate.IsOddInteger, degree.Value));
        Formula defined = Formula.And(radicand.DefinedWhen, degree.DefinedWhen, degreeNonzero, Formula.Or(positiveRadicand, zeroRadicand, negativeRadicand));
        return new SemanticExpression(value, defined, Formula.And(defined, Formula.Predicate(ExactPredicate.RootIsContinuous, radicand.Value, degree.Value)), Formula.And(defined, Formula.Predicate(ExactPredicate.RootIsDifferentiable, radicand.Value, degree.Value)), MergeProvenance(source, [radicand, degree]), [], [radicand, degree]);
    }

    private ValueTerm FoldBinary(ValueKind kind, ValueTerm left, ValueTerm right, out RewriteStep? rewrite)
    {
        rewrite = null;
        if (left.Kind == ValueKind.Constant && right.Kind == ValueKind.Constant)
        {
            BigRational? folded = kind switch
            {
                ValueKind.Add => left.Constant + right.Constant,
                ValueKind.Subtract => left.Constant - right.Constant,
                ValueKind.Multiply => left.Constant * right.Constant,
                ValueKind.Divide when !right.Constant.IsZero => left.Constant / right.Constant,
                _ => null
            };
            if (folded is not null)
            {
                _budget.CheckCoefficient(folded.Value);
                ValueTerm value = Constant(folded.Value);
                rewrite = new RewriteStep("exact-constant-fold", TermCanonical(kind, default, string.Empty, [left, right]), value.Canonical, Formula.True);
                return value;
            }
        }

        if (TryFoldExactIdentity(kind, left, right, out ValueTerm exactIdentity, out rewrite))
        {
            return exactIdentity;
        }

        if (kind == ValueKind.Multiply && ((left.Kind == ValueKind.Constant && left.Constant.IsZero) || (right.Kind == ValueKind.Constant && right.Constant.IsZero)))
        {
            ValueTerm zero = Constant(BigRational.Zero);
            rewrite = new RewriteStep("zero-product-value", TermCanonical(kind, default, string.Empty, [left, right]), zero.Canonical, Formula.True);
            return zero;
        }

        if (kind is ValueKind.Add or ValueKind.Subtract && right.Kind == ValueKind.Constant && right.Constant.IsZero)
        {
            rewrite = new RewriteStep("additive-identity", TermCanonical(kind, default, string.Empty, [left, right]), left.Canonical, Formula.True);
            return left;
        }

        if (kind == ValueKind.Add && left.Kind == ValueKind.Constant && left.Constant.IsZero)
        {
            rewrite = new RewriteStep("additive-identity", TermCanonical(kind, default, string.Empty, [left, right]), right.Canonical, Formula.True);
            return right;
        }

        if (kind == ValueKind.Multiply && right.Kind == ValueKind.Constant && right.Constant.IsOne)
        {
            rewrite = new RewriteStep("multiplicative-identity", TermCanonical(kind, default, string.Empty, [left, right]), left.Canonical, Formula.True);
            return left;
        }

        if (kind == ValueKind.Multiply && left.Kind == ValueKind.Constant && left.Constant.IsOne)
        {
            rewrite = new RewriteStep("multiplicative-identity", TermCanonical(kind, default, string.Empty, [left, right]), right.Canonical, Formula.True);
            return right;
        }

        if (kind == ValueKind.Divide && right.Kind == ValueKind.Constant && right.Constant.IsOne)
        {
            rewrite = new RewriteStep("multiplicative-identity", TermCanonical(kind, default, string.Empty, [left, right]), left.Canonical, Formula.True);
            return left;
        }

        return Term(kind, default, string.Empty, [left, right]);
    }

    private bool TryFoldExactIdentity(ValueKind kind, ValueTerm left, ValueTerm right, out ValueTerm value, out RewriteStep? rewrite)
    {
        string before = TermCanonical(kind, default, string.Empty, [left, right]);
        if (kind == ValueKind.Subtract && left.Id == right.Id)
        {
            value = Constant(BigRational.Zero);
            rewrite = new RewriteStep("self-subtraction-value", before, value.Canonical, Formula.True);
            return true;
        }

        if (kind == ValueKind.Divide && left.Id == right.Id)
        {
            value = Constant(BigRational.One);
            rewrite = new RewriteStep("self-division-value", before, value.Canonical, Formula.Compare(right, Comparison.NotEqual, Constant(BigRational.Zero)));
            return true;
        }

        if (kind is ValueKind.Add or ValueKind.Subtract && ExactScalar.TryCreate(left, _budget, out ExactScalar exactLeft) && ExactScalar.TryCreate(right, _budget, out ExactScalar exactRight) && ExactScalar.TryAdd(exactLeft, kind == ValueKind.Add ? exactRight : exactRight.Negate(), _budget, out ExactScalar exactSum) && exactSum.RationalValue is { } rationalSum)
        {
            value = Constant(rationalSum);
            rewrite = new RewriteStep("exact-scalar-fold", before, value.Canonical, Formula.True);
            return true;
        }

        value = null!;
        rewrite = null;
        return false;
    }

    private ValueTerm Constant(BigRational value) => Term(ValueKind.Constant, value, string.Empty, []);
    private ValueTerm VariableOrConstant(string name)
    {
        string normalized = NormalizeAsciiIdentifier(name);
        return normalized is "pi" or "e" ? Term(ValueKind.SymbolicConstant, default, normalized, []) : Term(ValueKind.Variable, default, normalized, []);
    }

    private static string NormalizeAsciiIdentifier(string value)
    {
        return string.Create(value.Length, value, static (destination, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];
                destination[index] = character is >= 'A' and <= 'Z'
                    ? (char)(character + ('a' - 'A'))
                    : character;
            }
        });
    }

    private ValueTerm Term(ValueKind kind, BigRational constant, string name, ImmutableArray<ValueTerm> operands)
    {
        string canonical = TermCanonical(kind, constant, name, operands);
        if (_terms.TryGetValue(canonical, out ValueTerm? existing))
        {
            return existing;
        }

        _budget.AddNode();
        var created = new ValueTerm(++_nextTermId, kind, constant, name, operands, canonical);
        _terms.Add(canonical, created);
        return created;
    }

    private static string TermCanonical(ValueKind kind, BigRational constant, string name, ImmutableArray<ValueTerm> operands)
    {
        if (kind == ValueKind.Constant)
        {
            return "q:" + constant.ToString();
        }

        if (kind == ValueKind.Variable)
        {
            return "v:" + name;
        }

        if (kind == ValueKind.SymbolicConstant)
        {
            return "c:" + name;
        }

        string arguments = string.Join(',', operands.Select(static operand => operand.Canonical));
        return string.Create(CultureInfo.InvariantCulture, $"{(int)kind}:{name}({arguments})");
    }

    private static ImmutableArray<SourceRange> MergeProvenance(SourceRange source, IEnumerable<SemanticExpression> operands) => operands.SelectMany(static operand => operand.Provenance).Append(source).Distinct().OrderBy(static value => value.Start).ThenBy(static value => value.Length).ToImmutableArray();
}
