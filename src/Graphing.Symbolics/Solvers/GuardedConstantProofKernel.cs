using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class GuardedConstantProofKernel
{
    public static bool TryCompute(GuardedConstantContext context, AnalysisFeatures feature, out object value)
    {
        value = feature switch
        {
            AnalysisFeatures.Domain => context.Domain,
            AnalysisFeatures.Range => RealSets.Points([context.Scalar.Value]),
            AnalysisFeatures.Parity => context.Symmetric ? context.Scalar.IsZero ? FunctionParity.Both : FunctionParity.Even : FunctionParity.Neither,
            AnalysisFeatures.Zeros => context.Scalar.IsZero ? context.Domain : EmptySet.Instance,
            AnalysisFeatures.YIntercept => context.ContainsZero ? OptionalValue<ExactReal>.Some(context.Scalar.Value) : OptionalValue<ExactReal>.None,
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima or AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes => ImmutableArray.Create(new Asymptote(AsymptoteOrientation.Horizontal, new SingletonReal(context.Scalar.Value), null, context.Scalar.Value)),
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(new MonotoneRegion(context.Domain, Monotonicity.Constant)),
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.PeriodicWithFundamentalPeriod, context.Period),
            _ => null!
        };
        return value is not null;
    }
}
