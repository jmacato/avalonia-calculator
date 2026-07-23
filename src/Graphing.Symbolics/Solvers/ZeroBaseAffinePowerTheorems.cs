using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class ZeroBaseAffinePowerTheorems
{
    public static bool TryCompute(ZeroBaseAffinePowerContext context, AnalysisFeatures feature, ResourceBudget budget, out object value)
    {
        budget.Charge();
        var zero = new RationalReal(BigRational.Zero);
        value = feature switch
        {
            AnalysisFeatures.Domain => context.Domain,
            AnalysisFeatures.Range => RealSets.Points([zero]),
            // A nonempty open half-line is not invariant under reflection.
            AnalysisFeatures.Parity => FunctionParity.Neither,
            AnalysisFeatures.Zeros => context.Domain,
            AnalysisFeatures.YIntercept => context.Intercept.Sign > 0 ? OptionalValue<ExactReal>.Some(zero) : OptionalValue<ExactReal>.None,
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes => ImmutableArray.Create(new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(zero), null, zero)),
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(new MonotoneRegion(context.Domain, Monotonicity.Constant)),
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        return value is not null;
    }
}
