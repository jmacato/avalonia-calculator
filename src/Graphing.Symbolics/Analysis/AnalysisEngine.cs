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
            SemanticExpression source = new SemanticGraphBuilder(budget).Build(request.Expression);
            expression = source;
            if (!SemanticSourceProjection.TryCreate(
                    source,
                    budget,
                    out SemanticExpression analysisExpression))
            {
                return AnalysisReportFactory.Unknown(
                    source,
                    request.Features,
                    UnknownReason.CertificateRejected,
                    budget.WorkUsed);
            }

            ProofOutcome<OptionalValue<ExactReal>> earlyOrigin =
                TryProveExactOriginEarly(request, analysisExpression, budget);
            ProofOutcome<RealSet> domain = Requested(request, AnalysisFeatures.Domain)
                ? SolveDomain(request, analysisExpression, budget)
                : ProofOutcome<RealSet>.Unknown(UnknownReason.NotRequested);

            RationalAnalysisContext? rational = null;
            try
            {
                if (!RationalAnalysisContext.TryCreate(
                        analysisExpression,
                        request.Variable,
                        budget,
                        out rational))
                {
                    rational = null;
                }
            }
            catch (BudgetExceededException) when (CanPreserveOnlyOrigin(
                       request,
                       earlyOrigin))
            {
                rational = null;
            }

            SemialgebraicUnaryContext? unary = null;
            if (rational is null)
            {
                try
                {
                    if (!SemialgebraicUnaryContext.TryCreate(
                            analysisExpression,
                            request.Variable,
                            budget,
                            out unary))
                    {
                        unary = null;
                    }
                }
                catch (BudgetExceededException) when (CanPreserveOnlyOrigin(
                           request,
                           earlyOrigin))
                {
                    unary = null;
                }
            }

            FixedRationalPowerAnalysis? fixedPower = null;
            if (rational is null && unary is null)
            {
                try
                {
                    if (FixedRationalPowerContext.TryCreate(
                            analysisExpression,
                            request.Variable,
                            budget,
                            out FixedRationalPowerContext powerContext))
                    {
                        fixedPower = new FixedRationalPowerAnalysis(
                            powerContext,
                            FixedRationalPowerProofKernel.BuildSignChart(powerContext, budget));
                    }
                }
                catch (BudgetExceededException)
                {
                    // The per-feature path below converts the same deterministic
                    // limit into Unknown(BudgetExceeded) without discarding a
                    // domain proof that may already have succeeded.
                    fixedPower = null;
                }
            }

            // Preserve the usual specialized-proof preference. If context
            // construction or a stale specialized proof consumed/rejected its
            // feature path, fall back to the already replayed local proof.
            ProofOutcome<OptionalValue<ExactReal>> yIntercept = Solve(
                request,
                analysisExpression,
                AnalysisFeatures.YIntercept,
                rational,
                unary,
                fixedPower,
                static (context, featureBudget) => RationalFeatureAnalyzer.YIntercept(context, featureBudget),
                budget);
            if (yIntercept.State == ProofState.Unknown &&
                earlyOrigin.State == ProofState.Proved)
            {
                yIntercept = earlyOrigin;
            }
            ProofOutcome<RealSet> range = Solve(
                request,
                analysisExpression,
                AnalysisFeatures.Range,
                rational,
                unary,
                fixedPower,
                static (context, featureBudget) => RationalFeatureAnalyzer.Range(context, featureBudget),
                budget);
            ProofOutcome<FunctionParity> parity = Solve(
                request,
                analysisExpression,
                AnalysisFeatures.Parity,
                rational,
                unary,
                fixedPower,
                static (context, featureBudget) => RationalFeatureAnalyzer.Parity(context, featureBudget),
                budget);
            ProofOutcome<RealSet> zeros = Solve(
                request,
                analysisExpression,
                AnalysisFeatures.Zeros,
                rational,
                unary,
                fixedPower,
                static (context, featureBudget) => RationalFeatureAnalyzer.Zeros(context, featureBudget),
                budget);
            ProofOutcome<ImmutableArray<FeaturePoint>> minima = Solve(
                request,
                analysisExpression,
                AnalysisFeatures.Minima,
                rational,
                unary,
                fixedPower,
                static (context, featureBudget) => RationalFeatureAnalyzer.Minima(context, featureBudget),
                budget);
            ProofOutcome<ImmutableArray<FeaturePoint>> maxima = Solve(
                request,
                analysisExpression,
                AnalysisFeatures.Maxima,
                rational,
                unary,
                fixedPower,
                static (context, featureBudget) => RationalFeatureAnalyzer.Maxima(context, featureBudget),
                budget);
            ProofOutcome<ImmutableArray<FeaturePoint>> inflections = Solve(
                request,
                analysisExpression,
                AnalysisFeatures.InflectionPoints,
                rational,
                unary,
                fixedPower,
                static (context, featureBudget) => RationalFeatureAnalyzer.Inflections(context, featureBudget),
                budget);
            ProofOutcome<ImmutableArray<Asymptote>> vertical = Solve(
                request,
                analysisExpression,
                AnalysisFeatures.VerticalAsymptotes,
                rational,
                unary,
                fixedPower,
                static (context, featureBudget) => RationalFeatureAnalyzer.VerticalAsymptotes(context, featureBudget),
                budget);
            ProofOutcome<ImmutableArray<Asymptote>> horizontal = Solve(
                request,
                analysisExpression,
                AnalysisFeatures.HorizontalAsymptotes,
                rational,
                unary,
                fixedPower,
                static (context, featureBudget) => RationalFeatureAnalyzer.HorizontalAsymptotes(context, featureBudget),
                budget);
            ProofOutcome<ImmutableArray<Asymptote>> oblique = Solve(
                request,
                analysisExpression,
                AnalysisFeatures.ObliqueAsymptotes,
                rational,
                unary,
                fixedPower,
                static (context, featureBudget) => RationalFeatureAnalyzer.ObliqueAsymptotes(context, featureBudget),
                budget);
            ProofOutcome<ImmutableArray<MonotoneRegion>> monotonicity = Solve(
                request,
                analysisExpression,
                AnalysisFeatures.Monotonicity,
                rational,
                unary,
                fixedPower,
                static (context, featureBudget) => RationalFeatureAnalyzer.Monotonicity(context, featureBudget),
                budget);
            ProofOutcome<Periodicity> period = Solve(
                request,
                analysisExpression,
                AnalysisFeatures.Period,
                rational,
                unary,
                fixedPower,
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

    private static bool CanPreserveOnlyOrigin(
        AnalysisRequest request,
        ProofOutcome<OptionalValue<ExactReal>> earlyOrigin)
    {
        return request.Features == AnalysisFeatures.YIntercept &&
               earlyOrigin.State == ProofState.Proved;
    }

    private static ProofOutcome<RealSet> SolveDomain(
        AnalysisRequest request,
        SemanticExpression expression,
        ResourceBudget budget)
    {
        if (DomainSolver.TrySolve(
                expression,
                request.Variable,
                budget,
                out RealSet domainValue,
                out DomainProofCertificate domainCertificate))
        {
            return CheckDomain(
                request,
                expression,
                ProofOutcome<RealSet>.Proved(domainValue, domainCertificate),
                budget);
        }

        if (BoundedRadicalTangentProductAnalyzer.TryAnalyze(
                request,
                expression,
                AnalysisFeatures.Domain,
                budget,
                out ProofOutcome<RealSet> boundedRadicalTangentDomain))
        {
            return CheckDomain(
                request,
                expression,
                boundedRadicalTangentDomain,
                budget);
        }

        if (AffineMinMaxAnalyzer.TryAnalyze(
                request,
                expression,
                AnalysisFeatures.Domain,
                budget,
                out ProofOutcome<RealSet> affineMinMaxDomain))
        {
            return CheckDomain(request, expression, affineMinMaxDomain, budget);
        }

        if (AffineSignAnalyzer.TryAnalyze(
                request,
                expression,
                AnalysisFeatures.Domain,
                budget,
                out ProofOutcome<RealSet> affineSignDomain))
        {
            return CheckDomain(request, expression, affineSignDomain, budget);
        }

        if (ZeroBaseAffinePowerAnalyzer.TryAnalyze(
                request,
                expression,
                AnalysisFeatures.Domain,
                budget,
                out ProofOutcome<RealSet> zeroBaseDomain))
        {
            return CheckDomain(request, expression, zeroBaseDomain, budget);
        }

        if (ZeroBaseTangentPowerAnalyzer.TryAnalyze(
                request,
                expression,
                AnalysisFeatures.Domain,
                budget,
                out ProofOutcome<RealSet> zeroBaseTangentDomain))
        {
            return CheckDomain(request, expression, zeroBaseTangentDomain, budget);
        }

        if (AffineFloorAnalyzer.TryAnalyze(
                request,
                expression,
                AnalysisFeatures.Domain,
                budget,
                out ProofOutcome<RealSet> affineFloorDomain))
        {
            return CheckDomain(request, expression, affineFloorDomain, budget);
        }

        if (GuardedCotangentIdentityAnalyzer.TryAnalyze(
                request,
                expression,
                AnalysisFeatures.Domain,
                budget,
                out ProofOutcome<RealSet> guardedCotangentDomain))
        {
            return CheckDomain(request, expression, guardedCotangentDomain, budget);
        }

        if (SingleHoleSineAnalyzer.TryAnalyze(
                request,
                expression,
                AnalysisFeatures.Domain,
                budget,
                out ProofOutcome<RealSet> singleHoleSineDomain))
        {
            return CheckDomain(request, expression, singleHoleSineDomain, budget);
        }

        if (ExactCoefficientAnalyzer.TryAnalyze(
                request,
                expression,
                AnalysisFeatures.Domain,
                budget,
                out ProofOutcome<RealSet> exactDomain))
        {
            return CheckDomain(request, expression, exactDomain, budget);
        }

        if (UnaryCompositionAnalyzer.TryAnalyze(
                request,
                expression,
                AnalysisFeatures.Domain,
                budget,
                out ProofOutcome<RealSet> compositionDomain))
        {
            return CheckDomain(request, expression, compositionDomain, budget);
        }

        if (AffinePhaseSineCompositionAnalyzer.TryAnalyze(
                request,
                expression,
                AnalysisFeatures.Domain,
                budget,
                out ProofOutcome<RealSet> affinePhaseSineCompositionDomain))
        {
            return CheckDomain(
                request,
                expression,
                affinePhaseSineCompositionDomain,
                budget);
        }

        if (ElementaryCompositionAnalyzer.TryAnalyze(
                request,
                expression,
                AnalysisFeatures.Domain,
                budget,
                out ProofOutcome<RealSet> elementaryDomain))
        {
            return CheckDomain(request, expression, elementaryDomain, budget);
        }

        if (GuardedConstantAnalyzer.TryAnalyze(
                request,
                expression,
                AnalysisFeatures.Domain,
                budget,
                out ProofOutcome<RealSet> guardedConstantDomain))
        {
            return CheckDomain(request, expression, guardedConstantDomain, budget);
        }

        if (TrigonometricAndLatticeAnalyzer.TryAnalyze(
                request,
                expression,
                AnalysisFeatures.Domain,
                budget,
                out ProofOutcome<RealSet> specializedDomain))
        {
            return CheckDomain(request, expression, specializedDomain, budget);
        }

        return ProofOutcome<RealSet>.Unknown(UnknownReason.UnsupportedFragment);
    }

    private static ProofOutcome<T> Solve<T>(
        AnalysisRequest request,
        SemanticExpression expression,
        AnalysisFeatures feature,
        RationalAnalysisContext? rational,
        SemialgebraicUnaryContext? unary,
        FixedRationalPowerAnalysis? fixedPower,
        Func<RationalAnalysisContext, ResourceBudget, ProofOutcome<T>> rationalSolver,
        ResourceBudget budget)
    {
        if (!Requested(request, feature))
        {
            return ProofOutcome<T>.Unknown(UnknownReason.NotRequested);
        }

        try
        {
            ProofOutcome<T> outcome;
            if (AffineMinMaxAnalyzer.TryAnalyze(
                    request,
                    expression,
                    feature,
                    budget,
                    out ProofOutcome<T>? affineMinMax))
            {
                outcome = affineMinMax;
            }
            else if (AffineSignAnalyzer.TryAnalyze(
                    request,
                    expression,
                    feature,
                    budget,
                    out ProofOutcome<T>? affineSign))
            {
                outcome = affineSign;
            }
            else if (ZeroBaseAffinePowerAnalyzer.TryAnalyze(
                         request,
                         expression,
                         feature,
                         budget,
                         out ProofOutcome<T>? zeroBasePower))
            {
                outcome = zeroBasePower;
            }
            else if (ZeroBaseTangentPowerAnalyzer.TryAnalyze(
                         request,
                         expression,
                         feature,
                         budget,
                         out ProofOutcome<T>? zeroBaseTangentPower))
            {
                outcome = zeroBaseTangentPower;
            }
            else if (AffineFloorAnalyzer.TryAnalyze(
                         request,
                         expression,
                         feature,
                         budget,
                         out ProofOutcome<T>? affineFloor))
            {
                outcome = affineFloor;
            }
            else if (GuardedCotangentIdentityAnalyzer.TryAnalyze(
                         request,
                         expression,
                         feature,
                         budget,
                         out ProofOutcome<T>? guardedCotangentIdentity))
            {
                outcome = guardedCotangentIdentity;
            }
            else if (SingleHoleSineAnalyzer.TryAnalyze(
                         request,
                         expression,
                         feature,
                         budget,
                         out ProofOutcome<T>? singleHoleSine))
            {
                outcome = singleHoleSine;
            }
            else if (BoundedRadicalTangentProductAnalyzer.TryAnalyze(
                         request,
                         expression,
                         feature,
                         budget,
                         out ProofOutcome<T>? boundedRadicalTangentProduct))
            {
                outcome = boundedRadicalTangentProduct;
            }
            else if (GuardedConstantAnalyzer.TryAnalyze(
                    request,
                    expression,
                    feature,
                    budget,
                    out ProofOutcome<T>? guardedConstant))
            {
                outcome = guardedConstant;
            }
            else if (AffineSquareLogAnalyzer.TryAnalyze(
                         request,
                         expression,
                         feature,
                         budget,
                         out ProofOutcome<T>? affineSquareLog))
            {
                outcome = affineSquareLog;
            }
            else if (UnaryCompositionAnalyzer.TryAnalyze(
                    request,
                    expression,
                    feature,
                    budget,
                    out ProofOutcome<T>? composition))
            {
                outcome = composition;
            }
            else if (AffinePhaseSineCompositionAnalyzer.TryAnalyze(
                         request,
                         expression,
                         feature,
                         budget,
                         out ProofOutcome<T>? affinePhaseSineComposition))
            {
                outcome = affinePhaseSineComposition;
            }
            else if (ElementaryCompositionAnalyzer.TryAnalyze(
                         request,
                         expression,
                         feature,
                         budget,
                         out ProofOutcome<T>? elementary))
            {
                outcome = elementary;
            }
            else if (rational is not null)
            {
                outcome = rationalSolver(rational, budget);
            }
            else if (unary is not null &&
                     SemialgebraicUnaryAnalyzer.TryAnalyze(
                         request,
                         unary,
                         feature,
                         budget,
                         out ProofOutcome<T> unaryOutcome))
            {
                outcome = unaryOutcome;
            }
            else if (fixedPower is not null &&
                     FixedRationalPowerAnalyzer.TryAnalyze(
                         request,
                         fixedPower,
                         feature,
                         budget,
                         out ProofOutcome<T>? cachedPower))
            {
                outcome = cachedPower;
            }
            else if (FixedRationalPowerAnalyzer.TryAnalyze(
                         request,
                         expression,
                         feature,
                         budget,
                         out ProofOutcome<T>? uncachedPower))
            {
                outcome = uncachedPower;
            }
            else if (AbsoluteCompositionAnalyzer.TryAnalyze(
                         request,
                         expression,
                         feature,
                         budget,
                         out ProofOutcome<T>? absolute))
            {
                outcome = absolute;
            }
            else if (ExactCoefficientAnalyzer.TryAnalyze(
                         request,
                         expression,
                         feature,
                         budget,
                         out ProofOutcome<T>? exactCoefficient))
            {
                outcome = exactCoefficient;
            }
            else if (CertifiedHarmonicRangeAnalyzer.TryAnalyze(
                         request,
                         expression,
                         feature,
                         budget,
                         out ProofOutcome<T>? harmonicRange))
            {
                outcome = harmonicRange;
            }
            else if (TryAnalyzeTrigonometric(
                         request,
                         expression,
                         feature,
                         budget,
                         out ProofOutcome<T>? trigonometric))
            {
                outcome = trigonometric;
            }
            else
            {
                outcome = ProofOutcome<T>.Unknown(UnknownReason.UnsupportedFragment);
            }

            return Check(request, expression, feature, outcome, budget);
        }
        catch (BudgetExceededException)
        {
            return ProofOutcome<T>.Unknown(UnknownReason.BudgetExceeded);
        }
    }

    private static bool TryAnalyzeTrigonometric<T>(
        AnalysisRequest request,
        SemanticExpression expression,
        AnalysisFeatures feature,
        ResourceBudget budget,
        out ProofOutcome<T> outcome)
    {
        if (TrigonometricAndLatticeAnalyzer.TryAnalyze(
                request,
                expression,
                feature,
                budget,
                out outcome))
        {
            return true;
        }

        return MonotoneTrigonometricPhaseAnalyzer.TryAnalyze(
            request,
            expression,
            feature,
            budget,
            out outcome);
    }

    private static ProofOutcome<OptionalValue<ExactReal>> TryProveExactOriginEarly(
        AnalysisRequest request,
        SemanticExpression expression,
        ResourceBudget budget)
    {
        if (!Requested(request, AnalysisFeatures.YIntercept))
        {
            return ProofOutcome<OptionalValue<ExactReal>>.Unknown(
                UnknownReason.NotRequested);
        }

        try
        {
            if (!ExactOriginAnalyzer.TryAnalyze(
                    request,
                    expression,
                    AnalysisFeatures.YIntercept,
                    budget,
                    out ProofOutcome<OptionalValue<ExactReal>>? outcome))
            {
                return ProofOutcome<OptionalValue<ExactReal>>.Unknown(
                    UnknownReason.UnsupportedFragment);
            }

            return Check(
                request,
                expression,
                AnalysisFeatures.YIntercept,
                outcome,
                budget);
        }
        catch (BudgetExceededException)
        {
            return ProofOutcome<OptionalValue<ExactReal>>.Unknown(
                UnknownReason.BudgetExceeded);
        }
    }

    private static ProofOutcome<T> Check<T>(
        AnalysisRequest request,
        SemanticExpression expression,
        AnalysisFeatures expectedFeature,
        ProofOutcome<T> outcome,
        ResourceBudget budget)
    {
        return outcome.State == ProofState.Unknown ||
               CertificateChecker.CheckProjected(
                   request,
                   expression,
                   expectedFeature,
                   outcome,
                   budget)
            ? outcome
            : ProofOutcome<T>.Unknown(UnknownReason.CertificateRejected);
    }

    private static ProofOutcome<RealSet> CheckDomain(
        AnalysisRequest request,
        SemanticExpression expression,
        ProofOutcome<RealSet> outcome,
        ResourceBudget budget)
    {
        return Check(request, expression, AnalysisFeatures.Domain, outcome, budget);
    }

    private static bool Requested(AnalysisRequest request, AnalysisFeatures feature)
    {
        return request.Features.HasFlag(feature);
    }
}
