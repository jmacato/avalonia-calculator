using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class FixedRationalPowerAnalyzer
{
    private const string Rule = "fixed-rational-power-sign-chart";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        if (!FixedRationalPowerContext.TryCreate(expression, request.Variable, budget, out FixedRationalPowerContext context))
        {
            outcome = null!;
            return false;
        }

        var analysis = new FixedRationalPowerAnalysis(context, FixedRationalPowerProofKernel.BuildSignChart(context, budget));
        return TryAnalyze(request, analysis, feature, budget, out outcome);
    }

    public static bool TryAnalyze<T>(AnalysisRequest request, FixedRationalPowerAnalysis analysis, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        FixedRationalPowerContext context = analysis.Context;
        CellDecompositionCertificate chart = analysis.SignChart;
        ImmutableArray<CellDecompositionCertificate> auxiliaries = feature == AnalysisFeatures.Parity ? FixedRationalPowerProofKernel.BuildParityCells(context, budget) : [];
        ImmutableArray<BigRational> boundaries = [];
        ImmutableArray<RationalPowerRangeFiberWitness> fibers = [];
        object value;
        if (feature == AnalysisFeatures.Range)
        {
            if (!FixedRationalPowerProofKernel.TryBuildRangeProof(context, chart, budget, out RealSet range, out boundaries, out fibers))
            {
                outcome = null!;
                return false;
            }

            value = range;
        }
        else if (!FixedRationalPowerProofKernel.TryComputeFeature(context, chart, feature, auxiliaries, budget, out value))
        {
            outcome = null!;
            return false;
        }

        if (value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new FixedRationalPowerProofCertificate(feature, context.Expression.Value.Canonical, ClaimCanonical.ForObject(value), context.Exponent, context.Basis.Numerator, context.Basis.Denominator, context.DomainFormula, chart, auxiliaries, boundaries, fibers, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }

    internal static bool IsRule(string rule) => rule == Rule;
}
