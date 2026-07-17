namespace Graphing.Symbolics;

internal static class SingleHarmonicRangeTheorems
{
    public static bool TryCompute(SingleHarmonicRangeContext context, ResourceBudget budget, out RealSet range)
    {
        budget.Charge();
        ExactScalar radiusSquared = ExactScalar.FromRational(context.RadiusSquared);
        if (!radiusSquared.TrySquareRoot(budget, out ExactScalar radius) || radius.Sign <= 0)
        {
            range = null!;
            return false;
        }

        ExactReal lower = ExactRealArithmetic.AddRational(radius.Negate().Value, context.Constant);
        ExactReal upper = ExactRealArithmetic.AddRational(radius.Value, context.Constant);
        range = new IntervalSet(RealBound.Finite(lower), true, RealBound.Finite(upper), true);
        return true;
    }
}
