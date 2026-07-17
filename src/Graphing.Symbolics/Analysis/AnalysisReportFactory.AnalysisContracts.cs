using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class AnalysisReportFactory
{
    public static AnalysisReport Unknown(SemanticExpression? expression, AnalysisFeatures requested, UnknownReason reason, long work) => new(expression, UnknownFor<RealSet>(requested, AnalysisFeatures.Domain, reason), UnknownFor<RealSet>(requested, AnalysisFeatures.Range, reason), UnknownFor<FunctionParity>(requested, AnalysisFeatures.Parity, reason), UnknownFor<RealSet>(requested, AnalysisFeatures.Zeros, reason), UnknownFor<OptionalValue<ExactReal>>(requested, AnalysisFeatures.YIntercept, reason), UnknownFor<ImmutableArray<FeaturePoint>>(requested, AnalysisFeatures.Minima, reason), UnknownFor<ImmutableArray<FeaturePoint>>(requested, AnalysisFeatures.Maxima, reason), UnknownFor<ImmutableArray<FeaturePoint>>(requested, AnalysisFeatures.InflectionPoints, reason), UnknownFor<ImmutableArray<Asymptote>>(requested, AnalysisFeatures.VerticalAsymptotes, reason), UnknownFor<ImmutableArray<Asymptote>>(requested, AnalysisFeatures.HorizontalAsymptotes, reason), UnknownFor<ImmutableArray<Asymptote>>(requested, AnalysisFeatures.ObliqueAsymptotes, reason), UnknownFor<ImmutableArray<MonotoneRegion>>(requested, AnalysisFeatures.Monotonicity, reason), UnknownFor<Periodicity>(requested, AnalysisFeatures.Period, reason), work);
    private static ProofOutcome<T> UnknownFor<T>(AnalysisFeatures requested, AnalysisFeatures feature, UnknownReason reason) => ProofOutcome<T>.Unknown(requested.HasFlag(feature) ? reason : UnknownReason.NotRequested);
}
