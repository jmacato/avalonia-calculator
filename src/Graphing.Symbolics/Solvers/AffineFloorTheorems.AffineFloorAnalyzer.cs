using System.Collections.Immutable;

namespace Graphing.Symbolics;
/// <summary>
/// Direct theorem instantiation for the exact real floor of a rational affine
/// expression. Every nonconstant level set is a maximal half-open interval;
/// the periodic interval family retains those cell boundaries explicitly.
/// </summary>
internal static class AffineFloorTheorems
{
    private const string IntegerParameter = "n";
    public static bool TryCompute(AffineFloorContext context, AnalysisFeatures feature, ResourceBudget budget, out object value)
    {
        budget.Charge();
        bool constant = context.Slope.IsZero;
        ExactReal constantValue = IntegerValue(context.Intercept.Floor());
        value = feature switch
        {
            AnalysisFeatures.Domain => AllRealSet.Instance,
            AnalysisFeatures.Range => constant ? RealSets.Points([constantValue]) : IntegerRange(),
            // No nonconstant affine floor can be even because its two tails
            // diverge with opposite signs. It cannot be odd either: on every
            // input interval [n,n+1), reflection produces an interval with
            // the opposite endpoint closure, so floor(b-t)=-floor(b+t)
            // already fails at one of the paired jump boundaries.
            AnalysisFeatures.Parity => constant ? context.Intercept.Floor().IsZero ? FunctionParity.Both : FunctionParity.Even : FunctionParity.Neither,
            AnalysisFeatures.Zeros => Zeros(context),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(constantValue),
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes => constant ? ImmutableArray.Create(Horizontal(constantValue)) : ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(new MonotoneRegion(constant ? AllRealSet.Instance : ConstantCells(context), Monotonicity.Constant)),
            AnalysisFeatures.Period => new Periodicity(constant ? PeriodicityKind.PeriodicWithoutFundamentalPeriod : PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        return value is not null;
    }

    private static Graphing.Symbolics.IntegerLatticeSet IntegerRange() => new IntegerLatticeSet(IntegerParameter, [IntegerParameter], [$"{IntegerParameter} ∈ ℤ"]);
    private static RealSet Zeros(AffineFloorContext context)
    {
        if (context.Slope.IsZero)
        {
            return context.Intercept.Floor().IsZero ? AllRealSet.Instance : EmptySet.Instance;
        }

        (BigRational lower, bool includesLower, BigRational upper, bool includesUpper) = ZeroCell(context);
        return Interval(lower, includesLower, upper, includesUpper);
    }

    private static PeriodicIntervalSet ConstantCells(AffineFloorContext context)
    {
        (BigRational lower, bool includesLower, BigRational upper, bool includesUpper) = ZeroCell(context);
        BigRational period = context.Slope.Sign > 0 ? context.Slope.Reciprocal() : -context.Slope.Reciprocal();
        return new PeriodicIntervalSet(Rational(period), IntegerParameter, IntegerConstraint.All(IntegerParameter), [new PeriodicInterval(Rational(lower), includesLower, Rational(upper), includesUpper)]);
    }

    private static (BigRational Lower, bool IncludesLower, BigRational Upper, bool IncludesUpper) ZeroCell(AffineFloorContext context)
    {
        BigRational zeroBoundary = -context.Intercept / context.Slope;
        BigRational oneBoundary = (BigRational.One - context.Intercept) / context.Slope;
        return context.Slope.Sign > 0 ? (zeroBoundary, true, oneBoundary, false) : (oneBoundary, false, zeroBoundary, true);
    }

    private static IntervalSet Interval(BigRational lower, bool includesLower, BigRational upper, bool includesUpper) => new(RealBound.Finite(Rational(lower)), includesLower, RealBound.Finite(Rational(upper)), includesUpper);
    private static Asymptote Horizontal(ExactReal value) => new(AsymptoteOrientation.Horizontal, new SingletonReal(value), null, value);
    private static RationalReal IntegerValue(ExactInteger value) => Rational(new BigRational(value));
    private static RationalReal Rational(BigRational value) => new(value);
}
