namespace Graphing.Symbolics;

/// <summary>
/// Checker-owned structural premises shared by affine sign and floor replay.
/// The source argument must be total; its possibly conservative regularity
/// metadata must propagate exactly through the outer primitive's
/// locally-constant guard.
/// </summary>
internal static class AffineDiscontinuousCertificateReplay
{
    public static bool TryGetArgument(
        SemanticExpression expression,
        string function,
        ResourceBudget budget,
        out SemanticExpression argument)
    {
        budget.Charge(8);
        if (expression.Value is not
            {
                Kind: ValueKind.Function,
                Operands: [var argumentValue]
            } primitive ||
            !string.Equals(primitive.Name, function, StringComparison.Ordinal) ||
            expression.SourceOperands is not [var sourceArgument] ||
            sourceArgument.Value.Id != argumentValue.Id ||
            !ExactFormulaVerifier.IsAlwaysTrue(sourceArgument.DefinedWhen, budget) ||
            !ExactFormulaVerifier.IsAlwaysTrue(expression.DefinedWhen, budget))
        {
            argument = null!;
            return false;
        }

        Formula locallyConstant = Formula.Predicate(
            ExactPredicate.IsLocallyConstant,
            expression.Value);
        if (!MatchesPropagation(
                expression.ContinuousWhen,
                sourceArgument.ContinuousWhen,
                locallyConstant) ||
            !MatchesPropagation(
                expression.DifferentiableWhen,
                sourceArgument.DifferentiableWhen,
                locallyConstant))
        {
            argument = null!;
            return false;
        }

        argument = sourceArgument;
        return true;
    }

    public static bool IsSingleFeature(AnalysisFeatures feature)
    {
        uint value = (uint)feature;
        return value != 0 &&
               (value & (value - 1)) == 0 &&
               (feature & AnalysisFeatures.All) == feature;
    }

    private static bool MatchesPropagation(
        Formula formula,
        Formula argumentCondition,
        Formula locallyConstant) =>
        string.Equals(
            formula.Canonical,
            Formula.And(argumentCondition, locallyConstant).Canonical,
            StringComparison.Ordinal);
}
