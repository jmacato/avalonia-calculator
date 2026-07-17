using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class GuardedCotangentIdentityTheorems
{
    public static bool TryCompute(GuardedCotangentIdentityContext context, AnalysisFeatures feature, out object value)
    {
        var zero = new RationalReal(BigRational.Zero);
        value = feature switch
        {
            AnalysisFeatures.Domain => context.Domain,
            AnalysisFeatures.Range => RealSets.Union(new IntervalSet(RealBound.NegativeInfinity, false, RealBound.Finite(zero), false), new IntervalSet(RealBound.Finite(zero), false, RealBound.PositiveInfinity, false)),
            AnalysisFeatures.Parity => FunctionParity.Odd,
            AnalysisFeatures.Zeros => EmptySet.Instance,
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.None,
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes => ImmutableArray.Create(new Asymptote(AsymptoteOrientation.Vertical, new PeriodicReal(zero, context.FunctionPeriod, "m", IntegerConstraint.All("m")), null, null)),
            AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(new MonotoneRegion(context.Domain, context.CotangentScale.Sign > 0 ? Monotonicity.Decreasing : Monotonicity.Increasing)),
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.PeriodicWithFundamentalPeriod, context.FunctionPeriod),
            _ => null!
        };
        return value is not null;
    }
}
