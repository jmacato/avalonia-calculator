using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class SemialgebraicUnaryAnalyzer
{
    private const string Rule = "semialgebraic-unary-rational-composition";
    public static bool TryAnalyze<T>(AnalysisRequest request, SemialgebraicUnaryContext context, AnalysisFeatures feature, ResourceBudget budget, out ProofOutcome<T> outcome)
    {
        UnarySignChartCertificate chart = UnaryCompositionProofKernel.BuildChart(context, budget);
        ImmutableArray<CellDecompositionCertificate> auxiliaries = feature == AnalysisFeatures.Parity ? UnaryCompositionProofKernel.BuildParityCells(context, budget) : [];
        ImmutableArray<BigRational> boundaries = [];
        ImmutableArray<UnaryRangeFiberWitness> fibers = [];
        object value;
        if (feature == AnalysisFeatures.Range)
        {
            if (!UnaryCompositionProofKernel.TryBuildRangeProof(context, chart, budget, out RealSet range, out boundaries, out fibers))
            {
                outcome = null!;
                return false;
            }

            value = range;
        }
        else if (!UnaryCompositionProofKernel.TryComputeFeature(context, chart, feature, auxiliaries, budget, out value))
        {
            outcome = null!;
            return false;
        }

        if (value is not T typed)
        {
            outcome = null!;
            return false;
        }

        var certificate = new SemialgebraicUnaryProofCertificate(feature, context.Expression.Value.Canonical, ClaimCanonical.ForObject(value), context.Kind, context.Inner.Numerator, context.Inner.Denominator, context.DomainFormula, chart, auxiliaries, boundaries, fibers, Rule);
        outcome = ProofOutcome<T>.Proved(typed, certificate);
        return true;
    }

    internal static bool IsRule(string rule)
    {
        return rule == Rule;
    }
}
