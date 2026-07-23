using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Projects source provenance through verified, domain-neutral arithmetic
/// identities.  The semantic value and every retained regularity condition
/// remain those of the original expression; only the source shape used by
/// theorem recognizers is canonicalized.
/// </summary>
internal static class SemanticSourceProjection
{
    public static bool TryCreate(SemanticExpression expression, ResourceBudget budget, out SemanticExpression projected)
    {
        var memo = new Dictionary<SemanticExpression, SemanticExpression>(ReferenceEqualityComparer.Instance);
        var active = new HashSet<SemanticExpression>(ReferenceEqualityComparer.Instance);
        return TryProject(expression, budget, memo, active, out projected);
    }

    private static bool TryProject(SemanticExpression expression, ResourceBudget budget, IDictionary<SemanticExpression, SemanticExpression> memo, ISet<SemanticExpression> active, out SemanticExpression projected)
    {
        budget.Charge();
        if (memo.TryGetValue(expression, out projected!))
        {
            return true;
        }

        if (!active.Add(expression) || expression.SourceOperands.IsDefault || expression.RewriteHistory.IsDefault)
        {
            projected = null!;
            return false;
        }

        var operands = ImmutableArray.CreateBuilder<SemanticExpression>(expression.SourceOperands.Length);
        foreach (SemanticExpression operand in expression.SourceOperands)
        {
            if (!TryProject(operand, budget, memo, active, out SemanticExpression child))
            {
                active.Remove(expression);
                projected = null!;
                return false;
            }

            operands.Add(child);
        }

        ImmutableArray<SemanticExpression> projectedOperands = operands.ToImmutable();
        SemanticSourceProjectionNeutralProjectionStatus status = ClassifyNeutralProjection(expression, budget, out SemanticExpression selected);
        if (status == SemanticSourceProjectionNeutralProjectionStatus.Invalid)
        {
            active.Remove(expression);
            projected = null!;
            return false;
        }

        if (status == SemanticSourceProjectionNeutralProjectionStatus.Selected)
        {
            SemanticExpression selectedProjection = projectedOperands[IndexOfReference(expression.SourceOperands, selected)];
            projected = selectedProjection with
            {
                Value = expression.Value,
                DefinedWhen = expression.DefinedWhen,
                ContinuousWhen = expression.ContinuousWhen,
                DifferentiableWhen = expression.DifferentiableWhen,
                Provenance = expression.Provenance
            };
        }
        else
        {
            projected = expression with
            {
                SourceOperands = projectedOperands
            };
        }

        active.Remove(expression);
        memo[expression] = projected;
        return true;
    }

    private static SemanticSourceProjectionNeutralProjectionStatus ClassifyNeutralProjection(SemanticExpression expression, ResourceBudget budget, out SemanticExpression selected)
    {
        budget.Charge();
        if (expression.RewriteHistory is not [var rewrite] || expression.SourceOperands is not [var left, var right])
        {
            selected = null!;
            return SemanticSourceProjectionNeutralProjectionStatus.NotApplicable;
        }

        bool hasSelection;
        switch (rewrite.Rule)
        {
            case "additive-identity":
                if (AreBothConstants(left.Value, right.Value) || !TryMatchOneOf(rewrite, left.Value, right.Value, out ValueKind additiveOperation, ValueKind.Add, ValueKind.Subtract) || !TrySelectNeutralOperand(additiveOperation, expression, left, right, out selected))
                {
                    selected = null!;
                    return SemanticSourceProjectionNeutralProjectionStatus.Invalid;
                }

                hasSelection = true;
                break;
            case "multiplicative-identity":
                if (AreBothConstants(left.Value, right.Value) || !TryMatchOneOf(rewrite, left.Value, right.Value, out ValueKind multiplicativeOperation, ValueKind.Multiply, ValueKind.Divide) || !TrySelectNeutralOperand(multiplicativeOperation, expression, left, right, out selected))
                {
                    selected = null!;
                    return SemanticSourceProjectionNeutralProjectionStatus.Invalid;
                }

                hasSelection = true;
                break;
            case "exact-constant-fold":
                if (!TryVerifyConstantFold(rewrite, expression.Value, left.Value, right.Value, out ValueKind foldedOperation))
                {
                    selected = null!;
                    return SemanticSourceProjectionNeutralProjectionStatus.Invalid;
                }

                hasSelection = TrySelectNeutralOperand(foldedOperation, expression, left, right, out selected);
                break;
            default:
                selected = null!;
                return SemanticSourceProjectionNeutralProjectionStatus.NotApplicable;
        }

        if (!SameFormula(rewrite.Guard, Formula.True) || !string.Equals(rewrite.After, expression.Value.Canonical, StringComparison.Ordinal) || !MatchesBinaryPropagation(expression, left, right))
        {
            selected = null!;
            return SemanticSourceProjectionNeutralProjectionStatus.Invalid;
        }

        if (!hasSelection)
        {
            return SemanticSourceProjectionNeutralProjectionStatus.NotApplicable;
        }

        return SameFormula(expression.DefinedWhen, selected.DefinedWhen) ? SemanticSourceProjectionNeutralProjectionStatus.Selected : SemanticSourceProjectionNeutralProjectionStatus.NotApplicable;
    }

    private static bool TrySelectNeutralOperand(ValueKind operation, SemanticExpression expression, SemanticExpression left, SemanticExpression right, out SemanticExpression selected)
    {
        ValueTerm result = expression.Value;
        bool leftZero = IsConstant(left.Value, BigRational.Zero);
        bool rightZero = IsConstant(right.Value, BigRational.Zero);
        bool leftOne = IsConstant(left.Value, BigRational.One);
        bool rightOne = IsConstant(right.Value, BigRational.One);
        var candidates = new List<SemanticExpression>(2);
        if (operation is ValueKind.Add or ValueKind.Subtract && rightZero && ReferenceEquals(result, left.Value))
        {
            candidates.Add(left);
        }

        if (operation == ValueKind.Add && leftZero && ReferenceEquals(result, right.Value))
        {
            candidates.Add(right);
        }

        if (operation == ValueKind.Multiply && leftOne && ReferenceEquals(result, right.Value))
        {
            candidates.Add(right);
        }

        if (operation is ValueKind.Multiply or ValueKind.Divide && rightOne && ReferenceEquals(result, left.Value))
        {
            candidates.Add(left);
        }

        foreach (SemanticExpression candidate in candidates)
        {
            if (SameFormula(expression.DefinedWhen, candidate.DefinedWhen))
            {
                selected = candidate;
                return true;
            }
        }

        if (candidates.Count != 0)
        {
            selected = candidates[0];
            return true;
        }

        selected = null!;
        return false;
    }

    private static bool TryVerifyConstantFold(RewriteStep rewrite, ValueTerm result, ValueTerm left, ValueTerm right, out ValueKind operation)
    {
        if (!AreBothConstants(left, right) || result.Kind != ValueKind.Constant || !TryMatchOneOf(rewrite, left, right, out operation, ValueKind.Add, ValueKind.Subtract, ValueKind.Multiply, ValueKind.Divide))
        {
            operation = default;
            return false;
        }

        BigRational? expected = operation switch
        {
            ValueKind.Add => left.Constant + right.Constant,
            ValueKind.Subtract => left.Constant - right.Constant,
            ValueKind.Multiply => left.Constant * right.Constant,
            ValueKind.Divide when !right.Constant.IsZero => left.Constant / right.Constant,
            _ => null
        };
        return expected == result.Constant;
    }

    private static bool MatchesBinaryPropagation(SemanticExpression expression, SemanticExpression left, SemanticExpression right)
    {
        return SameFormula(expression.DefinedWhen, Formula.And(left.DefinedWhen, right.DefinedWhen)) &&
               SameFormula(expression.ContinuousWhen, Formula.And(left.ContinuousWhen, right.ContinuousWhen)) &&
               SameFormula(expression.DifferentiableWhen,
                   Formula.And(left.DifferentiableWhen, right.DifferentiableWhen));
    }

    private static bool AreBothConstants(ValueTerm left, ValueTerm right)
    {
        return left.Kind == ValueKind.Constant && right.Kind == ValueKind.Constant;
    }

    private static bool IsConstant(ValueTerm value, BigRational expected)
    {
        return value.Kind == ValueKind.Constant && value.Constant == expected;
    }

    private static bool MatchesBefore(RewriteStep rewrite, ValueKind kind, ValueTerm left, ValueTerm right)
    {
        return string.Equals(rewrite.Before, $"{(int)kind}:({left.Canonical},{right.Canonical})",
            StringComparison.Ordinal);
    }

    private static bool TryMatchOneOf(RewriteStep rewrite, ValueTerm left, ValueTerm right, out ValueKind operation, params ValueKind[] candidates)
    {
        foreach (ValueKind candidate in candidates)
        {
            if (MatchesBefore(rewrite, candidate, left, right))
            {
                operation = candidate;
                return true;
            }
        }

        operation = default;
        return false;
    }

    private static bool SameFormula(Formula left, Formula right)
    {
        return string.Equals(left.Canonical, right.Canonical, StringComparison.Ordinal);
    }

    private static int IndexOfReference(ImmutableArray<SemanticExpression> operands, SemanticExpression selected)
    {
        for (int index = 0; index < operands.Length; index++)
        {
            if (ReferenceEquals(operands[index], selected))
            {
                return index;
            }
        }

        throw new InvalidOperationException("Selected source operand was not retained.");
    }
}
