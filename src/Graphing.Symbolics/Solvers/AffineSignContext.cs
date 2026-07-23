namespace Graphing.Symbolics;
/// <summary>
/// Exact normalized premises for <c>sign(a*x + b)</c>. Coefficients may be
/// rational or exact symbolic scalars, but their signs must be proved by the
/// exact-scalar kernel. Partial affine arguments are deliberately rejected.
/// </summary>
internal sealed record AffineSignContext(SemanticExpression Expression, SemanticExpression Argument, ExactScalar Slope, ExactScalar Intercept, string NormalizedArgumentCanonical)
{
    public static bool TryCreate(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out AffineSignContext context)
    {
        budget.Charge();
        if (expression.Value is not { Kind: ValueKind.Function, Name: "sign", Operands.Length: 1 } function || expression.SourceOperands.Length != 1 || !string.Equals(function.Operands[0].Canonical, expression.SourceOperands[0].Value.Canonical, StringComparison.Ordinal))
        {
            context = null!;
            return false;
        }

        SemanticExpression argument = expression.SourceOperands[0];
        if (!ExactRationalExtractor.TryExtract(argument.Value, variable, budget, out ExactRationalExtraction extraction) || !extraction.DomainExclusions.IsEmpty || !extraction.Denominator.IsConstant || extraction.Denominator[0].IsZero || extraction.Numerator.Degree > 1 || !TrigonometricAndLatticeAnalyzer.DomainMatches(argument, variable, angleUnit, AllRealSet.Instance, budget) || !TrigonometricAndLatticeAnalyzer.DomainMatches(expression, variable, angleUnit, AllRealSet.Instance, budget))
        {
            context = null!;
            return false;
        }

        ExactScalar reciprocal = extraction.Denominator[0].Reciprocal(budget);
        ExactScalar slope = extraction.Numerator[1].Multiply(reciprocal, budget);
        ExactScalar intercept = extraction.Numerator[0].Multiply(reciprocal, budget);
        string normalized = $"affine-sign[{slope.Canonical},{intercept.Canonical}]";
        context = new AffineSignContext(expression, argument, slope, intercept, normalized);
        return true;
    }
}
