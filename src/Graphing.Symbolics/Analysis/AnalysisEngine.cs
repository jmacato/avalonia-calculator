using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class AnalysisEngine
{
    public static AnalysisReport Analyze(AnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var budget = new ResourceBudget(request.RevisionIsCurrent);
        SemanticExpression? expression = null;
        try
        {
            expression = new SemanticGraphBuilder(budget).Build(request.Expression);
            bool hasDomain = DomainSolver.TrySolve(
                expression,
                request.Variable,
                budget,
                out RealSet domainValue,
                out DomainProofCertificate domainCertificate);
            ProofOutcome<RealSet> domain = Requested(request, AnalysisFeatures.Domain)
                ? hasDomain
                    ? Check(
                        request,
                        expression,
                        ProofOutcome<RealSet>.Proved(domainValue, domainCertificate))
                    : TrigonometricAndLatticeAnalyzer.TryAnalyze(
                        request,
                        expression,
                        AnalysisFeatures.Domain,
                        budget,
                        out ProofOutcome<RealSet> specializedDomain)
                        ? Check(request, expression, specializedDomain)
                        : ProofOutcome<RealSet>.Unknown(UnknownReason.UnsupportedFragment)
                : ProofOutcome<RealSet>.Unknown(UnknownReason.NotRequested);

            RationalAnalysisContext? rational = null;
            if (!RationalAnalysisContext.TryCreate(
                    expression,
                    request.Variable,
                    budget,
                    out rational))
            {
                rational = null;
            }

            ProofOutcome<RealSet> range = Solve(
                request,
                expression,
                AnalysisFeatures.Range,
                rational,
                static (context, featureBudget) => RationalFeatureAnalyzer.Range(context, featureBudget),
                budget);
            ProofOutcome<FunctionParity> parity = Solve(
                request,
                expression,
                AnalysisFeatures.Parity,
                rational,
                static (context, featureBudget) => RationalFeatureAnalyzer.Parity(context, featureBudget),
                budget);
            ProofOutcome<RealSet> zeros = Solve(
                request,
                expression,
                AnalysisFeatures.Zeros,
                rational,
                static (context, featureBudget) => RationalFeatureAnalyzer.Zeros(context, featureBudget),
                budget);
            ProofOutcome<OptionalValue<ExactReal>> yIntercept = Solve(
                request,
                expression,
                AnalysisFeatures.YIntercept,
                rational,
                static (context, featureBudget) => RationalFeatureAnalyzer.YIntercept(context, featureBudget),
                budget);
            ProofOutcome<ImmutableArray<FeaturePoint>> minima = Solve(
                request,
                expression,
                AnalysisFeatures.Minima,
                rational,
                static (context, featureBudget) => RationalFeatureAnalyzer.Minima(context, featureBudget),
                budget);
            ProofOutcome<ImmutableArray<FeaturePoint>> maxima = Solve(
                request,
                expression,
                AnalysisFeatures.Maxima,
                rational,
                static (context, featureBudget) => RationalFeatureAnalyzer.Maxima(context, featureBudget),
                budget);
            ProofOutcome<ImmutableArray<FeaturePoint>> inflections = Solve(
                request,
                expression,
                AnalysisFeatures.InflectionPoints,
                rational,
                static (context, featureBudget) => RationalFeatureAnalyzer.Inflections(context, featureBudget),
                budget);
            ProofOutcome<ImmutableArray<Asymptote>> vertical = Solve(
                request,
                expression,
                AnalysisFeatures.VerticalAsymptotes,
                rational,
                static (context, featureBudget) => RationalFeatureAnalyzer.VerticalAsymptotes(context, featureBudget),
                budget);
            ProofOutcome<ImmutableArray<Asymptote>> horizontal = Solve(
                request,
                expression,
                AnalysisFeatures.HorizontalAsymptotes,
                rational,
                static (context, featureBudget) => RationalFeatureAnalyzer.HorizontalAsymptotes(context, featureBudget),
                budget);
            ProofOutcome<ImmutableArray<Asymptote>> oblique = Solve(
                request,
                expression,
                AnalysisFeatures.ObliqueAsymptotes,
                rational,
                static (context, featureBudget) => RationalFeatureAnalyzer.ObliqueAsymptotes(context, featureBudget),
                budget);
            ProofOutcome<ImmutableArray<MonotoneRegion>> monotonicity = Solve(
                request,
                expression,
                AnalysisFeatures.Monotonicity,
                rational,
                static (context, featureBudget) => RationalFeatureAnalyzer.Monotonicity(context, featureBudget),
                budget);
            ProofOutcome<Periodicity> period = Solve(
                request,
                expression,
                AnalysisFeatures.Period,
                rational,
                static (context, featureBudget) => RationalFeatureAnalyzer.Period(context, featureBudget),
                budget);

            return new AnalysisReport(
                expression,
                domain,
                range,
                parity,
                zeros,
                yIntercept,
                minima,
                maxima,
                inflections,
                vertical,
                horizontal,
                oblique,
                monotonicity,
                period,
                budget.WorkUsed);
        }
        catch (BudgetExceededException)
        {
            return AnalysisReportFactory.Unknown(
                expression,
                request.Features,
                UnknownReason.BudgetExceeded,
                budget.WorkUsed);
        }
    }

    private static ProofOutcome<T> Solve<T>(
        AnalysisRequest request,
        SemanticExpression expression,
        AnalysisFeatures feature,
        RationalAnalysisContext? rational,
        Func<RationalAnalysisContext, ResourceBudget, ProofOutcome<T>> rationalSolver,
        ResourceBudget budget)
    {
        if (!Requested(request, feature))
        {
            return ProofOutcome<T>.Unknown(UnknownReason.NotRequested);
        }

        try
        {
            ProofOutcome<T> outcome = rational is not null
                ? rationalSolver(rational, budget)
                : TrigonometricAndLatticeAnalyzer.TryAnalyze(
                    request,
                    expression,
                    feature,
                    budget,
                    out ProofOutcome<T>? specialized)
                    ? specialized
                    : ProofOutcome<T>.Unknown(UnknownReason.UnsupportedFragment);
            return Check(request, expression, outcome);
        }
        catch (BudgetExceededException)
        {
            return ProofOutcome<T>.Unknown(UnknownReason.BudgetExceeded);
        }
    }

    private static ProofOutcome<T> Check<T>(
        AnalysisRequest request,
        SemanticExpression expression,
        ProofOutcome<T> outcome) =>
        outcome.State == ProofState.Unknown || CertificateChecker.Check(request, expression, outcome)
            ? outcome
            : ProofOutcome<T>.Unknown(UnknownReason.CertificateRejected);

    private static bool Requested(AnalysisRequest request, AnalysisFeatures feature) =>
        request.Features.HasFlag(feature);
}
