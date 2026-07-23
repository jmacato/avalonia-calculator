namespace Graphing.Symbolics;

internal sealed record AffineReciprocalTrigZeroContext(AffineReciprocalTrigPattern Pattern, ExactScalar DenominatorTarget, int UnitIntervalComparison, RealSet Domain)
{
    public static bool TryCreate(AnalysisRequest request, SemanticExpression expression, ResourceBudget budget, out AffineReciprocalTrigZeroContext context)
    {
        budget.Charge();
        if (!AffineReciprocalTrigonometricAnalyzer.TryGetPattern(expression.Value, request.Variable, budget, out AffineReciprocalTrigPattern pattern) || pattern.Shift.IsZero)
        {
            context = null!;
            return false;
        }

        RealSet domain = AffineReciprocalTrigonometricAnalyzer.Domain(pattern, request.AngleUnit, budget);
        if (!TrigonometricAndLatticeAnalyzer.DomainMatches(expression, request.Variable, request.AngleUnit, domain, budget))
        {
            context = null!;
            return false;
        }

        ExactScalar denominatorTarget = pattern.Amplitude.Negate().Multiply(ExactScalar.FromRational(pattern.Shift).Reciprocal(budget), budget);
        if (denominatorTarget.IsZero)
        {
            // A zero target would violate the retained sec/csc denominator
            // guard and the cotangent-to-tangent equivalence. The recognized
            // family has nonzero amplitude and shift, so reject if this
            // invariant is ever broken by a future normalization change.
            context = null!;
            return false;
        }

        int comparison = 0;
        if (pattern.Function is "sec" or "csc" && !denominatorTarget.TryCompareAbsoluteTo(BigRational.One, budget, out comparison))
        {
            context = null!;
            return false;
        }

        context = new AffineReciprocalTrigZeroContext(pattern, denominatorTarget, comparison, domain);
        return true;
    }
}
