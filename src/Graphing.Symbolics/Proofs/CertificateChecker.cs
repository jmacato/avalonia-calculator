using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class CertificateChecker
{
    public static bool Check<T>(
        AnalysisRequest request,
        SemanticExpression expression,
        ProofOutcome<T> outcome)
    {
        if (outcome.State == ProofState.Unknown)
        {
            return outcome.Certificate is null;
        }

        if (outcome.Certificate is null || outcome.Value is null)
        {
            return false;
        }

        string claim;
        try
        {
            claim = ClaimCanonical.For(outcome.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        if (!string.Equals(claim, outcome.Certificate.ClaimCanonical, StringComparison.Ordinal) ||
            !string.Equals(
                expression.Value.Canonical,
                outcome.Certificate.SubjectCanonical,
                StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var budget = new ResourceBudget(request.RevisionIsCurrent);
            return outcome.Certificate switch
            {
                DomainProofCertificate domain => CheckDomain(
                    request,
                    expression,
                    domain,
                    claim,
                    budget),
                RationalFunctionProofCertificate rational => CheckRational(
                    request,
                    expression,
                    rational,
                    claim,
                    budget),
                TheoremProofCertificate theorem => TheoremCertificateChecker.Check(
                    request,
                    expression,
                    theorem,
                    claim,
                    budget),
                _ => false
            };
        }
        catch (BudgetExceededException)
        {
            return false;
        }
    }

    private static bool CheckDomain(
        AnalysisRequest request,
        SemanticExpression expression,
        DomainProofCertificate certificate,
        string claim,
        ResourceBudget budget)
    {
        if (certificate.Feature != AnalysisFeatures.Domain ||
            !string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) ||
            !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) ||
            certificate.Rule != "univariate-semialgebraic-definedness" ||
            certificate.Cells is null ||
            !string.Equals(
                certificate.DefinednessFormula,
                expression.DefinedWhen.Canonical,
                StringComparison.Ordinal) ||
            !PolynomialFormulaConverter.TryConvert(
                expression.DefinedWhen,
                request.Variable,
                budget,
                out PolynomialFormula formula) ||
            !string.Equals(
                formula.Canonical,
                certificate.Cells.Formula.Canonical,
                StringComparison.Ordinal) ||
            !CellDecomposer.Verify(certificate.Cells, budget))
        {
            return false;
        }

        return string.Equals(certificate.Cells.Result.Canonical, claim, StringComparison.Ordinal);
    }

    private static bool CheckRational(
        AnalysisRequest request,
        SemanticExpression expression,
        RationalFunctionProofCertificate certificate,
        string claim,
        ResourceBudget budget)
    {
        if (!RationalAnalysisContext.TryCreate(
                expression,
                request.Variable,
                budget,
                out RationalAnalysisContext context) ||
            !string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) ||
            !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) ||
            !context.Function.Numerator.Equals(certificate.Numerator) ||
            !context.Function.Denominator.Equals(certificate.Denominator) ||
            !SamePolynomials(
                context.Extraction.DomainExclusions,
                certificate.OriginalDomainExclusions) ||
            !RuleMatchesFeature(certificate.Feature, certificate.Rule))
        {
            return false;
        }

        foreach (RootIsolationCertificate isolation in certificate.RootIsolations)
        {
            if (!SturmRootIsolator.Verify(isolation, budget))
            {
                return false;
            }
        }

        string recomputed;
        switch (certificate.Feature)
        {
            case AnalysisFeatures.Zeros:
                recomputed = ClaimCanonical.For(
                    RationalFeatureAnalyzer.ComputeZeros(context, budget, out _));
                break;
            case AnalysisFeatures.YIntercept:
                recomputed = ClaimCanonical.For(
                    RationalFeatureAnalyzer.ComputeYIntercept(context, budget));
                break;
            case AnalysisFeatures.Parity:
                recomputed = ClaimCanonical.For(
                    RationalFeatureAnalyzer.ComputeParity(context, budget, out _));
                break;
            case AnalysisFeatures.Range:
                if (!RationalFeatureAnalyzer.TryComputeRange(context, budget, out RealSet range, out _))
                {
                    return false;
                }

                recomputed = ClaimCanonical.For(range);
                break;
            case AnalysisFeatures.Minima:
                RationalFeatureAnalyzer.ComputeExtrema(context, budget, out ImmutableArray<FeaturePoint> minima,
                    out _, out _);
                recomputed = ClaimCanonical.For(minima);
                break;
            case AnalysisFeatures.Maxima:
                RationalFeatureAnalyzer.ComputeExtrema(context, budget, out _,
                    out ImmutableArray<FeaturePoint> maxima, out _);
                recomputed = ClaimCanonical.For(maxima);
                break;
            case AnalysisFeatures.InflectionPoints:
                recomputed = ClaimCanonical.For(
                    RationalFeatureAnalyzer.ComputeInflections(context, budget, out _));
                break;
            case AnalysisFeatures.Monotonicity:
                recomputed = ClaimCanonical.For(
                    RationalFeatureAnalyzer.ComputeMonotonicity(context, budget, out _));
                break;
            case AnalysisFeatures.VerticalAsymptotes:
                recomputed = ClaimCanonical.For(
                    RationalFeatureAnalyzer.ComputeVerticalAsymptotes(context, budget, out _));
                break;
            case AnalysisFeatures.HorizontalAsymptotes:
                recomputed = ClaimCanonical.For(
                    RationalFeatureAnalyzer.ComputeHorizontalAsymptotes(context));
                break;
            case AnalysisFeatures.ObliqueAsymptotes:
                recomputed = ClaimCanonical.For(
                    RationalFeatureAnalyzer.ComputeObliqueAsymptotes(context, budget));
                break;
            case AnalysisFeatures.Period:
                recomputed = ClaimCanonical.For(RationalFeatureAnalyzer.ComputePeriod(context));
                break;
            default:
                return false;
        }

        return string.Equals(recomputed, claim, StringComparison.Ordinal);
    }

    private static bool RuleMatchesFeature(AnalysisFeatures feature, string rule) => feature switch
    {
        AnalysisFeatures.Zeros => rule == "rational-zero-cell-decomposition",
        AnalysisFeatures.YIntercept => rule == "rational-origin-substitution",
        AnalysisFeatures.Parity => rule == "symmetric-domain-rational-identity",
        AnalysisFeatures.Range => rule == "certified-rational-range-projection",
        AnalysisFeatures.Minima or AnalysisFeatures.Maxima =>
            rule == "derivative-sign-cell-classification",
        AnalysisFeatures.InflectionPoints =>
            rule == "two-sided-second-derivative-sign-change",
        AnalysisFeatures.Monotonicity =>
            rule == "maximal-domain-derivative-sign-cells",
        AnalysisFeatures.VerticalAsymptotes => rule == "reduced-denominator-poles",
        AnalysisFeatures.HorizontalAsymptotes => rule == "rational-degree-limit",
        AnalysisFeatures.ObliqueAsymptotes => rule == "rational-polynomial-division-limit",
        AnalysisFeatures.Period => rule == "nonconstant-rational-functions-have-no-real-period",
        _ => false
    };

    private static bool SamePolynomials(
        ImmutableArray<UnivariatePolynomial> left,
        ImmutableArray<UnivariatePolynomial> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Length; index++)
        {
            if (!left[index].Equals(right[index]))
            {
                return false;
            }
        }

        return true;
    }
}

internal static class TheoremCertificateChecker
{
    public static bool Check(
        AnalysisRequest request,
        SemanticExpression expression,
        TheoremProofCertificate certificate,
        string claim,
        ResourceBudget budget)
    {
        if (!string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) ||
            !string.Equals(certificate.Claim, claim, StringComparison.Ordinal))
        {
            return false;
        }

        return TrigonometricAndLatticeAnalyzer.VerifyTheorem(
            request,
            expression,
            certificate,
            claim,
            budget);
    }
}
