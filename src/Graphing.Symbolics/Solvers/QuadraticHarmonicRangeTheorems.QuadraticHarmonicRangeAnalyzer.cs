namespace Graphing.Symbolics;

internal static class QuadraticHarmonicRangeTheorems
{
    public static bool TryCompute(QuadraticHarmonicRangeContext context, ResourceBudget budget, out RealSet range)
    {
        budget.Charge();
        BigRational quadratic = context.QuadraticCoefficient;
        BigRational linear = context.LinearCoefficient;
        BigRational constant = context.Constant;
        BigRational atNegativeOne = quadratic - linear + constant;
        BigRational atPositiveOne = quadratic + linear + constant;
        budget.CheckCoefficient(atNegativeOne);
        budget.CheckCoefficient(atPositiveOne);
        BigRational lower = Min(atNegativeOne, atPositiveOne);
        BigRational upper = Max(atNegativeOne, atPositiveOne);
        BigRational vertex = -linear / (new BigRational(2) * quadratic);
        budget.CheckCoefficient(vertex);
        if (vertex >= BigRational.MinusOne && vertex <= BigRational.One)
        {
            BigRational vertexValue = constant - ((linear * linear) / (new BigRational(4) * quadratic));
            budget.CheckCoefficient(vertexValue);
            lower = Min(lower, vertexValue);
            upper = Max(upper, vertexValue);
        }

        range = new IntervalSet(RealBound.Finite(new RationalReal(lower)), true, RealBound.Finite(new RationalReal(upper)), true);
        return true;
    }

    private static BigRational Min(BigRational first, BigRational second) => first <= second ? first : second;
    private static BigRational Max(BigRational first, BigRational second) => first >= second ? first : second;
}
