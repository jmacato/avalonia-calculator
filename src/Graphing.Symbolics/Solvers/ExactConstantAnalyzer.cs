using System.Collections.Immutable;

namespace Graphing.Symbolics;

/// <summary>
/// Proves the public properties of an exactly represented constant function.
/// Exact transcendental values remain outside the rational-polynomial
/// coefficient ring; this analyzer consumes only the sidecar value and sign
/// evidence supplied by <see cref="ExactScalar"/>.
/// </summary>
internal static class ExactConstantAnalyzer
{
    public static bool TryCompute(
        SemanticExpression expression,
        string variable,
        AngleUnit angleUnit,
        AnalysisFeatures feature,
        ResourceBudget budget,
        out object value,
        out ImmutableArray<string> parameters)
    {
        budget.Charge();
        if (!ExactScalar.TryCreate(expression.Value, budget, out ExactScalar scalar) ||
            !TrigonometricAndLatticeAnalyzer.DomainMatches(
                expression,
                variable,
                angleUnit,
                AllRealSet.Instance,
                budget))
        {
            value = null!;
            parameters = [];
            return false;
        }

        value = feature switch
        {
            AnalysisFeatures.Domain => AllRealSet.Instance,
            AnalysisFeatures.Range => RealSets.Points([scalar.Value]),
            AnalysisFeatures.Parity => scalar.IsZero
                ? FunctionParity.Both
                : FunctionParity.Even,
            AnalysisFeatures.Zeros => scalar.IsZero
                ? AllRealSet.Instance
                : EmptySet.Instance,
            AnalysisFeatures.YIntercept => OptionalValue<ExactReal>.Some(scalar.Value),
            AnalysisFeatures.Minima or
            AnalysisFeatures.Maxima or
            AnalysisFeatures.InflectionPoints => ImmutableArray<FeaturePoint>.Empty,
            AnalysisFeatures.VerticalAsymptotes or
            AnalysisFeatures.ObliqueAsymptotes => ImmutableArray<Asymptote>.Empty,
            AnalysisFeatures.HorizontalAsymptotes => ImmutableArray.Create(
                new Asymptote(
                    AsymptoteOrientation.Horizontal,
                    new SingletonReal(scalar.Value),
                    null,
                    scalar.Value)),
            AnalysisFeatures.Monotonicity => ImmutableArray.Create(
                new MonotoneRegion(AllRealSet.Instance, Monotonicity.Constant)),
            AnalysisFeatures.Period => new Periodicity(
                PeriodicityKind.PeriodicWithoutFundamentalPeriod,
                null),
            _ => null!
        };
        parameters =
        [
            "exact-constant",
            scalar.Canonical,
            expression.DefinedWhen.Canonical,
            angleUnit.ToString()
        ];
        return value is not null;
    }
}
