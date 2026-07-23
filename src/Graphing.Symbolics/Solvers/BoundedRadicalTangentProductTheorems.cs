using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class BoundedRadicalTangentProductTheorems
{
    public static bool TryCompute(BoundedRadicalTangentProductContext context, AnalysisFeatures feature, out object value)
    {
        var zero = new RationalReal(BigRational.Zero);
        var upper = new RationalReal(context.UpperEndpoint);
        value = feature switch
        {
            AnalysisFeatures.Domain => context.Domain,
            AnalysisFeatures.Parity => FunctionParity.Neither,
            AnalysisFeatures.Zeros => RealSets.Points([zero, upper]),
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(zero),
            AnalysisFeatures.VerticalAsymptotes or AnalysisFeatures.HorizontalAsymptotes or AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.Period => new Periodicity(PeriodicityKind.NotPeriodic, null),
            _ => null!
        };
        return value is not null;
    }
}
