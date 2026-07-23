namespace Graphing.Symbolics;
/// <summary>
/// Exact normalized premises for <c>floor(a*x + b)</c>. This first proof
/// kernel deliberately accepts rational affine arguments only. In
/// particular, it rejects algebraically affine values whose source graph has
/// a retained hole instead of silently extending their domain.
/// </summary>
internal sealed record AffineFloorContext(SemanticExpression Expression, SemanticExpression Argument, BigRational Slope, BigRational Intercept, string NormalizedArgumentCanonical)
{
    public static bool TryCreate(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out AffineFloorContext context)
    {
        budget.Charge();
        if (expression.Value is not { Kind: ValueKind.Function, Name: "floor", Operands.Length: 1 } function || expression.SourceOperands.Length != 1 || !string.Equals(function.Operands[0].Canonical, expression.SourceOperands[0].Value.Canonical, StringComparison.Ordinal))
        {
            context = null!;
            return false;
        }

        SemanticExpression argument = expression.SourceOperands[0];
        if (!RationalFunctionExtractor.TryExtract(argument.Value, variable, budget, out RationalExtraction extraction) || !extraction.DomainExclusions.IsEmpty || !extraction.Function.Denominator.IsConstant || extraction.Function.Denominator[0].IsZero || extraction.Function.Numerator.Degree > 1 || !TrigonometricAndLatticeAnalyzer.DomainMatches(argument, variable, angleUnit, AllRealSet.Instance, budget) || !TrigonometricAndLatticeAnalyzer.DomainMatches(expression, variable, angleUnit, AllRealSet.Instance, budget))
        {
            context = null!;
            return false;
        }

        BigRational reciprocal = extraction.Function.Denominator[0].Reciprocal();
        BigRational slope = extraction.Function.Numerator[1] * reciprocal;
        BigRational intercept = extraction.Function.Numerator[0] * reciprocal;
        budget.CheckCoefficient(slope);
        budget.CheckCoefficient(intercept);
        context = new AffineFloorContext(expression, argument, slope, intercept, $"affine-floor[{slope},{intercept}]");
        return true;
    }
}
