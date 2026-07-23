namespace Graphing.Symbolics;
/// <summary>
/// Exact premises for <c>0^(a*x+b)</c> with a nonzero total affine exponent.
/// The coefficients may be rational or supported exact symbolic scalars, but
/// their signs must be proved by the exact-scalar kernel. The nonzero-slope
/// restriction keeps this theorem disjoint from the existing exact-constant
/// and degenerate-domain paths.
/// </summary>
internal sealed record ZeroBaseAffinePowerContext(SemanticExpression Expression, SemanticExpression Exponent, ExactScalar Slope, ExactScalar Intercept, RealSet Domain)
{
    public static bool TryCreate(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out ZeroBaseAffinePowerContext context)
    {
        budget.Charge();
        if (expression.Value is not { Kind: ValueKind.Power, Operands: [var basisValue, var exponentValue] } || expression.SourceOperands is not [var basis, var exponent] || basis.Value.Id != basisValue.Id || exponent.Value.Id != exponentValue.Id || !ExactScalar.TryCreate(basis.Value, budget, out ExactScalar scalarBasis) || !scalarBasis.IsZero || !TrigonometricAndLatticeAnalyzer.DomainMatches(basis, variable, angleUnit, AllRealSet.Instance, budget) || !ExactRationalExtractor.TryExtract(exponent.Value, variable, budget, out ExactRationalExtraction extraction) || !extraction.DomainExclusions.IsEmpty || !extraction.Denominator.IsConstant || extraction.Denominator[0].IsZero || extraction.Numerator.Degree > 1 || !TrigonometricAndLatticeAnalyzer.DomainMatches(exponent, variable, angleUnit, AllRealSet.Instance, budget))
        {
            context = null!;
            return false;
        }

        ExactScalar reciprocal = extraction.Denominator[0].Reciprocal(budget);
        ExactScalar slope = extraction.Numerator[1].Multiply(reciprocal, budget);
        ExactScalar intercept = extraction.Numerator[0].Multiply(reciprocal, budget);
        if (slope.IsZero)
        {
            context = null!;
            return false;
        }

        ExactScalar thresholdScalar = slope.Sign > 0 ? intercept.Negate().Multiply(slope.Reciprocal(budget), budget) : intercept.Multiply(slope.Negate().Reciprocal(budget), budget);
        ExactReal threshold = thresholdScalar.Value;
        RealSet domain = slope.Sign > 0 ? new IntervalSet(RealBound.Finite(threshold), false, RealBound.PositiveInfinity, false) : new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(threshold), false);
        if (!MatchesLockedZeroBaseDefinedness(expression, basis, exponent, budget))
        {
            context = null!;
            return false;
        }

        context = new ZeroBaseAffinePowerContext(expression, exponent, slope, intercept, domain);
        return true;
    }

    private static bool MatchesLockedZeroBaseDefinedness(SemanticExpression expression, SemanticExpression basis, SemanticExpression exponent, ResourceBudget budget)
    {
        budget.Charge();
        Formula expected = Formula.Compare(exponent.Value, Comparison.Greater, basis.Value);
        return string.Equals(expression.DefinedWhen.Canonical, expected.Canonical, StringComparison.Ordinal);
    }
}
