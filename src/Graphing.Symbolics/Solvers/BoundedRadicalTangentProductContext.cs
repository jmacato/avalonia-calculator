using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Exact premises for <c>sqrt(x) * sqrt(c - x) * tan(x)</c>. This fragment is
/// deliberately source-sensitive: guarded identities, hidden holes, alternate
/// radical spellings, and value-only rewrites do not satisfy the premises.
/// </summary>
internal sealed record BoundedRadicalTangentProductContext(SemanticExpression Expression, SemanticExpression Variable, SemanticExpression LowerRadical, SemanticExpression UpperRadical, SemanticExpression Tangent, BigRational UpperEndpoint, IntervalSet Domain, string SourceShapeCanonical)
{
    private static readonly ValueTerm Zero = new(-1, ValueKind.Constant, BigRational.Zero, string.Empty, [], "q:0");
    public ImmutableArray<string> FactorCanonicals => [LowerRadical.Value.Canonical, UpperRadical.Value.Canonical, Tangent.Value.Canonical];

    public static bool TryCreate(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out BoundedRadicalTangentProductContext context)
    {
        budget.Charge();
        if (angleUnit != AngleUnit.Radians || !TryFlattenExactMultiplication(expression, budget, out ImmutableArray<SemanticExpression> factors, out string sourceShape) || factors.Length != 3)
        {
            context = null!;
            return false;
        }

        SemanticExpression? variableExpression = null;
        SemanticExpression? lowerRadical = null;
        SemanticExpression? upperRadical = null;
        SemanticExpression? tangent = null;
        BigRational upperEndpoint = default;
        string upperEndpointSourceShape = string.Empty;
        foreach (SemanticExpression factor in factors)
        {
            budget.Charge();
            if (TryMatchLowerRadical(factor, variable, out SemanticExpression lowerVariable))
            {
                if (lowerRadical is not null || !SameVariable(variableExpression, lowerVariable))
                {
                    context = null!;
                    return false;
                }

                variableExpression = lowerVariable;
                lowerRadical = factor;
                continue;
            }

            if (TryMatchUpperRadical(factor, variable, budget, out SemanticExpression upperVariable, out BigRational candidateEndpoint, out string candidateEndpointSourceShape))
            {
                if (upperRadical is not null || !SameVariable(variableExpression, upperVariable))
                {
                    context = null!;
                    return false;
                }

                variableExpression = upperVariable;
                upperRadical = factor;
                upperEndpoint = candidateEndpoint;
                upperEndpointSourceShape = candidateEndpointSourceShape;
                continue;
            }

            if (TryMatchTangent(factor, variable, out SemanticExpression tangentVariable))
            {
                if (tangent is not null || !SameVariable(variableExpression, tangentVariable))
                {
                    context = null!;
                    return false;
                }

                variableExpression = tangentVariable;
                tangent = factor;
                continue;
            }

            context = null!;
            return false;
        }

        if (variableExpression is null || lowerRadical is null || upperRadical is null || tangent is null || upperEndpoint.Sign <= 0 || upperEndpoint > BigRational.One)
        {
            context = null!;
            return false;
        }

        // pi > 2 is an exact classical bound. Hence c <= 1 < pi/2, so cos(x)
        // is strictly positive throughout [0,c]. The tangent guard contributes
        // no puncture to the two radical inequalities 0 <= x <= c.
        Formula expectedDefinedness = Formula.And(lowerRadical.DefinedWhen, upperRadical.DefinedWhen, tangent.DefinedWhen);
        Formula expectedContinuity = Formula.And(lowerRadical.ContinuousWhen, upperRadical.ContinuousWhen, tangent.ContinuousWhen);
        Formula expectedDifferentiability = Formula.And(lowerRadical.DifferentiableWhen, upperRadical.DifferentiableWhen, tangent.DifferentiableWhen);
        if (!CanonicalEquals(expression.DefinedWhen, expectedDefinedness) || !CanonicalEquals(expression.ContinuousWhen, expectedContinuity) || !CanonicalEquals(expression.DifferentiableWhen, expectedDifferentiability))
        {
            context = null!;
            return false;
        }

        var domain = new IntervalSet(RealBound.Finite(new RationalReal(BigRational.Zero)), true, RealBound.Finite(new RationalReal(upperEndpoint)), true);
        context = new BoundedRadicalTangentProductContext(expression, variableExpression, lowerRadical, upperRadical, tangent, upperEndpoint, domain, $"{sourceShape};upper-endpoint[{upperEndpointSourceShape}]");
        return true;
    }

    private static bool TryFlattenExactMultiplication(SemanticExpression expression, ResourceBudget budget, out ImmutableArray<SemanticExpression> factors, out string sourceShape)
    {
        var factorBuilder = ImmutableArray.CreateBuilder<SemanticExpression>();
        if (!TryFlatten(expression, budget, factorBuilder, out sourceShape))
        {
            factors = [];
            return false;
        }

        factors = factorBuilder.ToImmutable();
        return true;
    }

    private static bool TryFlatten(SemanticExpression expression, ResourceBudget budget, ImmutableArray<SemanticExpression>.Builder factors, out string shape)
    {
        budget.Charge();
        if (expression.Value.Kind != ValueKind.Multiply)
        {
            factors.Add(expression);
            shape = $"factor[{expression.Value.Canonical}]";
            return true;
        }

        if (expression.Value.Operands is not [var leftValue, var rightValue] || expression.SourceOperands is not [var left, var right] || left.Value.Id != leftValue.Id || right.Value.Id != rightValue.Id || !expression.RewriteHistory.IsEmpty || !CanonicalEquals(expression.DefinedWhen, Formula.And(left.DefinedWhen, right.DefinedWhen)) || !CanonicalEquals(expression.ContinuousWhen, Formula.And(left.ContinuousWhen, right.ContinuousWhen)) || !CanonicalEquals(expression.DifferentiableWhen, Formula.And(left.DifferentiableWhen, right.DifferentiableWhen)) || !TryFlatten(left, budget, factors, out string leftShape) || !TryFlatten(right, budget, factors, out string rightShape))
        {
            shape = string.Empty;
            return false;
        }

        shape = $"multiply[{leftShape},{rightShape}]";
        return true;
    }

    private static bool TryMatchLowerRadical(SemanticExpression expression, string variable, out SemanticExpression variableExpression)
    {
        if (!TryMatchSquareRoot(expression, out SemanticExpression radicand) || !IsBareVariable(radicand, variable))
        {
            variableExpression = null!;
            return false;
        }

        variableExpression = radicand;
        return true;
    }

    private static bool TryMatchUpperRadical(SemanticExpression expression, string variable, ResourceBudget budget, out SemanticExpression variableExpression, out BigRational upperEndpoint, out string upperEndpointSourceShape)
    {
        if (!TryMatchSquareRoot(expression, out SemanticExpression radicand) || radicand.Value is not { Kind: ValueKind.Subtract, Operands: [var constantValue, var variableValue] } || radicand.SourceOperands is not [var constant, var candidateVariable] || constant.Value.Id != constantValue.Id || candidateVariable.Value.Id != variableValue.Id || !radicand.RewriteHistory.IsEmpty || !TryEvaluateExactRationalSource(constant, budget, out upperEndpoint, out upperEndpointSourceShape) || !IsBareVariable(candidateVariable, variable) || !HasInheritedRegularity(radicand, constant, candidateVariable))
        {
            variableExpression = null!;
            upperEndpoint = default;
            upperEndpointSourceShape = string.Empty;
            return false;
        }

        variableExpression = candidateVariable;
        return true;
    }

    private static bool TryMatchSquareRoot(SemanticExpression expression, out SemanticExpression radicand)
    {
        if (expression.Value is not { Kind: ValueKind.Function, Name: "sqrt", Operands: [var radicandValue] } || expression.SourceOperands is not [var sourceRadicand] || sourceRadicand.Value.Id != radicandValue.Id || !expression.RewriteHistory.IsEmpty)
        {
            radicand = null!;
            return false;
        }

        Formula nonnegative = Formula.Compare(sourceRadicand.Value, Comparison.GreaterOrEqual, Zero);
        Formula positive = Formula.Compare(sourceRadicand.Value, Comparison.Greater, Zero);
        if (!CanonicalEquals(expression.DefinedWhen, Formula.And(sourceRadicand.DefinedWhen, nonnegative)) || !CanonicalEquals(expression.ContinuousWhen, Formula.And(sourceRadicand.ContinuousWhen, nonnegative)) || !CanonicalEquals(expression.DifferentiableWhen, Formula.And(sourceRadicand.DifferentiableWhen, positive)))
        {
            radicand = null!;
            return false;
        }

        radicand = sourceRadicand;
        return true;
    }

    private static bool TryMatchTangent(SemanticExpression expression, string variable, out SemanticExpression variableExpression)
    {
        if (expression.Value is not { Kind: ValueKind.Function, Name: "tan", Operands: [var argumentValue] } || expression.SourceOperands is not [var argument] || argument.Value.Id != argumentValue.Id || !expression.RewriteHistory.IsEmpty || !IsBareVariable(argument, variable))
        {
            variableExpression = null!;
            return false;
        }

        ValueTerm cosine = FunctionTerm("cos", argument.Value);
        Formula tangentDefined = Formula.Compare(cosine, Comparison.NotEqual, Zero);
        if (!CanonicalEquals(expression.DefinedWhen, Formula.And(argument.DefinedWhen, tangentDefined)) || !CanonicalEquals(expression.ContinuousWhen, Formula.And(argument.ContinuousWhen, tangentDefined)) || !CanonicalEquals(expression.DifferentiableWhen, Formula.And(argument.DifferentiableWhen, tangentDefined)))
        {
            variableExpression = null!;
            return false;
        }

        variableExpression = argument;
        return true;
    }

    private static bool IsBareVariable(SemanticExpression expression, string variable)
    {
        return expression.Value is { Kind: ValueKind.Variable, Name: var name, Operands.IsEmpty: true } &&
               name.Equals(variable, StringComparison.OrdinalIgnoreCase) && expression.SourceOperands.IsEmpty &&
               expression.RewriteHistory.IsEmpty && CanonicalEquals(expression.DefinedWhen, Formula.True) &&
               CanonicalEquals(expression.ContinuousWhen, Formula.True) &&
               CanonicalEquals(expression.DifferentiableWhen, Formula.True);
    }

    private static bool TryEvaluateExactRationalSource(SemanticExpression expression, ResourceBudget budget, out BigRational value, out string sourceShape)
    {
        budget.Charge();
        if (expression.Value is not { Kind: ValueKind.Constant, Operands.IsEmpty: true } constant)
        {
            value = default;
            sourceShape = string.Empty;
            return false;
        }

        budget.CheckCoefficient(constant.Constant);
        if (expression.SourceOperands.IsEmpty)
        {
            if (!expression.RewriteHistory.IsEmpty || !HasExactEverywhereRegularity(expression))
            {
                value = default;
                sourceShape = string.Empty;
                return false;
            }

            value = constant.Constant;
            sourceShape = $"rational[{constant.Canonical}]";
            return true;
        }

        if (expression.SourceOperands is [var operand] && TryEvaluateExactRationalSource(operand, budget, out BigRational operandValue, out string operandShape) && TryMatchExactConstantRewrite(expression, "exact-unary-constant-fold", SourceTermCanonical(ValueKind.Negate, operand.Value)) && CanonicalEquals(expression.DefinedWhen, operand.DefinedWhen) && CanonicalEquals(expression.ContinuousWhen, operand.ContinuousWhen) && CanonicalEquals(expression.DifferentiableWhen, operand.DifferentiableWhen))
        {
            BigRational expected = -operandValue;
            budget.CheckCoefficient(expected);
            if (constant.Constant == expected)
            {
                value = expected;
                sourceShape = $"negate[{operandShape}]";
                return true;
            }
        }

        if (expression.SourceOperands is [var left, var right] && TryEvaluateExactRationalSource(left, budget, out BigRational leftValue, out string leftShape) && TryEvaluateExactRationalSource(right, budget, out BigRational rightValue, out string rightShape) && HasInheritedRegularity(expression, left, right))
        {
            foreach (ValueKind kind in ExactRationalBinaryKinds)
            {
                budget.Charge();
                if (!TryMatchExactConstantRewrite(expression, "exact-constant-fold", SourceTermCanonical(kind, left.Value, right.Value)) || !TryEvaluateBinary(kind, leftValue, rightValue, budget, out BigRational expected))
                {
                    continue;
                }

                if (constant.Constant == expected)
                {
                    value = expected;
                    sourceShape = $"{BinaryKindName(kind)}[{leftShape},{rightShape}]";
                    return true;
                }
            }
        }

        value = default;
        sourceShape = string.Empty;
        return false;
    }

    private static ImmutableArray<ValueKind> ExactRationalBinaryKinds { get; } = [ValueKind.Add, ValueKind.Subtract, ValueKind.Multiply, ValueKind.Divide];

    private static string BinaryKindName(ValueKind kind)
    {
        return kind switch
        {
            ValueKind.Add => "add",
            ValueKind.Subtract => "subtract",
            ValueKind.Multiply => "multiply",
            ValueKind.Divide => "divide",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static bool TryEvaluateBinary(ValueKind kind, BigRational left, BigRational right, ResourceBudget budget, out BigRational value)
    {
        if (kind == ValueKind.Divide && right.IsZero)
        {
            value = default;
            return false;
        }

        value = kind switch
        {
            ValueKind.Add => left + right,
            ValueKind.Subtract => left - right,
            ValueKind.Multiply => left * right,
            ValueKind.Divide => left / right,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        budget.CheckCoefficient(value);
        return true;
    }

    private static bool TryMatchExactConstantRewrite(SemanticExpression expression, string rule, string before)
    {
        return expression.RewriteHistory is [var rewrite] &&
               string.Equals(rewrite.Rule, rule, StringComparison.Ordinal) &&
               string.Equals(rewrite.Before, before, StringComparison.Ordinal) &&
               string.Equals(rewrite.After, expression.Value.Canonical, StringComparison.Ordinal) &&
               rewrite.Guard == Formula.True;
    }

    private static bool HasExactEverywhereRegularity(SemanticExpression expression)
    {
        return CanonicalEquals(expression.DefinedWhen, Formula.True) &&
               CanonicalEquals(expression.ContinuousWhen, Formula.True) &&
               CanonicalEquals(expression.DifferentiableWhen, Formula.True);
    }

    private static string SourceTermCanonical(ValueKind kind, params ValueTerm[] operands)
    {
        return $"{(int)kind}:({string.Join(',', operands.Select(static value => value.Canonical))})";
    }

    private static bool HasInheritedRegularity(SemanticExpression expression, SemanticExpression left, SemanticExpression right)
    {
        return CanonicalEquals(expression.DefinedWhen, Formula.And(left.DefinedWhen, right.DefinedWhen)) &&
               CanonicalEquals(expression.ContinuousWhen, Formula.And(left.ContinuousWhen, right.ContinuousWhen)) &&
               CanonicalEquals(expression.DifferentiableWhen,
                   Formula.And(left.DifferentiableWhen, right.DifferentiableWhen));
    }

    private static bool SameVariable(SemanticExpression? established, SemanticExpression candidate)
    {
        return established is null || established.Value.Id == candidate.Value.Id;
    }

    private static bool CanonicalEquals(Formula actual, Formula expected)
    {
        return string.Equals(actual.Canonical, expected.Canonical, StringComparison.Ordinal);
    }

    private static ValueTerm FunctionTerm(string name, ValueTerm argument)
    {
        return new ValueTerm(-1, ValueKind.Function, default, name, [argument],
            $"{(int)ValueKind.Function}:{name}({argument.Canonical})");
    }
}
