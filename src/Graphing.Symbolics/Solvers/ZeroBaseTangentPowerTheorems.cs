using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class ZeroBaseTangentPowerTheorems
{
    public static bool TryCompute(ZeroBaseTangentPowerContext context, AnalysisFeatures feature, out object value)
    {
        var zero = new RationalReal(BigRational.Zero);
        value = feature switch
        {
            AnalysisFeatures.Domain => context.Domain,
            AnalysisFeatures.Range => RealSets.Points([zero]),
            // Centered tangent is odd, so its strict-positive domain reflects
            // to the disjoint strict-negative domain rather than to itself.
            AnalysisFeatures.Parity => FunctionParity.Neither,
            AnalysisFeatures.Zeros => context.Domain,
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.None,
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes => ImmutableArray.Create(new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(zero), null, zero)),
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(new MonotoneRegion(context.Domain, Monotonicity.Constant)),
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.PeriodicWithFundamentalPeriod, context.Period),
            _ => null!
        };
        return value is not null;
    }
}
