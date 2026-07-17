using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record AnalysisReport(SemanticExpression? Expression, ProofOutcome<RealSet> Domain, ProofOutcome<RealSet> Range, ProofOutcome<FunctionParity> Parity, ProofOutcome<RealSet> Zeros, ProofOutcome<OptionalValue<ExactReal>> YIntercept, ProofOutcome<ImmutableArray<FeaturePoint>> Minima, ProofOutcome<ImmutableArray<FeaturePoint>> Maxima, ProofOutcome<ImmutableArray<FeaturePoint>> InflectionPoints, ProofOutcome<ImmutableArray<Asymptote>> VerticalAsymptotes, ProofOutcome<ImmutableArray<Asymptote>> HorizontalAsymptotes, ProofOutcome<ImmutableArray<Asymptote>> ObliqueAsymptotes, ProofOutcome<ImmutableArray<MonotoneRegion>> Monotonicity, ProofOutcome<Periodicity> Period, long ChargedWorkUnits);
