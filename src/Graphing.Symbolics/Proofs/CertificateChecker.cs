using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class CertificateChecker
{
    public static bool Check<T>(AnalysisRequest request, SemanticExpression expression, ProofOutcome<T> outcome)
    {
        try
        {
            return Check(request, expression, outcome, new ResourceBudget(request.RevisionIsCurrent));
        }
        catch (BudgetExceededException)
        {
            return false;
        }
    }

    internal static bool Check<T>(AnalysisRequest request, SemanticExpression expression, ProofOutcome<T> outcome, ResourceBudget budget)
    {
        return CheckCore(request, expression, outcome, budget, projectSource: true);
    }

    private static bool CheckCore<T>(AnalysisRequest request, SemanticExpression expression, ProofOutcome<T> outcome, ResourceBudget budget, bool projectSource)
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

        if (!string.Equals(claim, outcome.Certificate.ClaimCanonical, StringComparison.Ordinal) || !string.Equals(expression.Value.Canonical, outcome.Certificate.SubjectCanonical, StringComparison.Ordinal))
        {
            return false;
        }

        SemanticExpression replayExpression = expression;
        if (projectSource && !SemanticSourceProjection.TryCreate(expression, budget, out replayExpression))
        {
            return false;
        }

        return CheckFoundationalCertificate(request, replayExpression, outcome.Certificate, claim, budget) ?? CheckSpecializedCertificate(request, replayExpression, outcome.Certificate, claim, budget) ?? false;
    }

    internal static bool Check<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures expectedFeature, ProofOutcome<T> outcome, ResourceBudget budget)
    {
        if (outcome.State != ProofState.Unknown && outcome.Certificate?.Feature != expectedFeature)
        {
            return false;
        }

        return Check(request, expression, outcome, budget);
    }

    internal static bool CheckProjected<T>(AnalysisRequest request, SemanticExpression expression, AnalysisFeatures expectedFeature, ProofOutcome<T> outcome, ResourceBudget budget)
    {
        if (outcome.State != ProofState.Unknown && outcome.Certificate?.Feature != expectedFeature)
        {
            return false;
        }

        return CheckCore(request, expression, outcome, budget, projectSource: false);
    }

    private static bool? CheckFoundationalCertificate(AnalysisRequest request, SemanticExpression expression, ProofCertificate certificate, string claim, ResourceBudget budget)
    {
        return certificate switch
        {
            DomainProofCertificate domain => CheckDomain(request, expression, domain, claim, budget),
            RationalFunctionProofCertificate rational => CheckRational(request, expression, rational, claim, budget),
            RationalRangeProofCertificate range => CheckRange(request, expression, range, claim, budget),
            OddDegreeDenominatorRangeProofCertificate oddDenominatorRange => OddDegreeDenominatorRangeCertificateChecker.Check(request, expression, oddDenominatorRange, claim, budget),
            SemialgebraicUnaryProofCertificate unary => SemialgebraicUnaryCertificateChecker.Check(request, expression, unary, claim, budget),
            FixedRationalPowerProofCertificate fixedPower => FixedRationalPowerCertificateChecker.Check(request, expression, fixedPower, claim, budget),
            UnaryCompositionProofCertificate composition => UnaryCompositionCertificateChecker.Check(request, expression, composition, claim, budget),
            AffinePhaseSineCompositionProofCertificate affinePhaseSineComposition => AffinePhaseSineCompositionCertificateChecker.Check(request, expression, affinePhaseSineComposition, claim, budget),
            ElementaryCompositionProofCertificate elementary => ElementaryCompositionCertificateChecker.Check(request, expression, elementary, claim, budget),
            MonotoneTrigonometricPhaseProofCertificate phase => MonotoneTrigonometricPhaseCertificateChecker.Check(request, expression, phase, claim, budget),
            GuardedConstantProofCertificate guardedConstant => GuardedConstantCertificateChecker.Check(request, expression, guardedConstant, claim, budget),
            _ => null
        };
    }

    private static bool? CheckSpecializedCertificate(AnalysisRequest request, SemanticExpression expression, ProofCertificate certificate, string claim, ResourceBudget budget)
    {
        return certificate switch
        {
            AffineMinMaxProofCertificate affineMinMax => AffineMinMaxCertificateChecker.Check(request, expression, affineMinMax, claim, budget),
            AffineSignProofCertificate affineSign => AffineSignCertificateChecker.Check(request, expression, affineSign, claim, budget),
            ZeroBaseAffinePowerProofCertificate zeroBasePower => ZeroBaseAffinePowerCertificateChecker.Check(request, expression, zeroBasePower, claim, budget),
            ZeroBaseTangentPowerProofCertificate zeroBaseTangentPower => ZeroBaseTangentPowerCertificateChecker.Check(request, expression, zeroBaseTangentPower, claim, budget),
            AffineFloorProofCertificate affineFloor => AffineFloorCertificateChecker.Check(request, expression, affineFloor, claim, budget),
            GuardedCotangentIdentityProofCertificate guardedCotangentIdentity => GuardedCotangentIdentityCertificateChecker.Check(request, expression, guardedCotangentIdentity, claim, budget),
            SingleHoleSineProofCertificate singleHoleSine => SingleHoleSineCertificateChecker.Check(request, expression, singleHoleSine, claim, budget),
            BoundedRadicalTangentProductProofCertificate boundedRadicalTangentProduct => BoundedRadicalTangentProductCertificateChecker.Check(request, expression, boundedRadicalTangentProduct, claim, budget),
            AffineReciprocalTrigZeroProofCertificate affineReciprocalTrigZero => AffineReciprocalTrigZeroCertificateChecker.Check(request, expression, affineReciprocalTrigZero, claim, budget),
            SingleHarmonicRangeProofCertificate singleHarmonicRange => SingleHarmonicRangeCertificateChecker.Check(request, expression, singleHarmonicRange, claim, budget),
            QuadraticHarmonicRangeProofCertificate quadraticHarmonicRange => QuadraticHarmonicRangeCertificateChecker.Check(request, expression, quadraticHarmonicRange, claim, budget),
            AffineSquareLogProofCertificate affineSquareLog => AffineSquareLogCertificateChecker.Check(request, expression, affineSquareLog, claim, budget),
            AbsoluteCompositionProofCertificate absolute => AbsoluteCompositionCertificateChecker.Check(request, expression, absolute, claim, budget),
            ExactCoefficientProofCertificate exactCoefficient => ExactCoefficientCertificateChecker.Check(request, expression, exactCoefficient, claim, budget),
            ExactOriginProofCertificate exactOrigin => ExactOriginCertificateReplay.Check(request, expression, exactOrigin, claim, budget),
            TheoremProofCertificate theorem => TheoremCertificateChecker.Check(request, expression, theorem, claim, budget),
            _ => null
        };
    }

    private static bool CheckRange(AnalysisRequest request, SemanticExpression expression, RationalRangeProofCertificate certificate, string claim, ResourceBudget budget)
    {
        if (!string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal))
        {
            return false;
        }

        return RationalRangeProjection.Verify(request, expression, certificate, claim, budget);
    }

    private static bool CheckDomain(AnalysisRequest request, SemanticExpression expression, DomainProofCertificate certificate, string claim, ResourceBudget budget)
    {
        if (certificate.Feature != AnalysisFeatures.Domain || !string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || certificate.Rule != "univariate-semialgebraic-definedness" || certificate.Cells is null || !string.Equals(certificate.DefinednessFormula, expression.DefinedWhen.Canonical, StringComparison.Ordinal) || !PolynomialFormulaConverter.TryConvert(expression.DefinedWhen, request.Variable, budget, out PolynomialFormula formula) || !string.Equals(formula.Canonical, certificate.Cells.Formula.Canonical, StringComparison.Ordinal) || !CellDecomposer.Verify(certificate.Cells, budget))
        {
            return false;
        }

        return string.Equals(certificate.Cells.Result.Canonical, claim, StringComparison.Ordinal);
    }

    private static bool CheckRational(AnalysisRequest request, SemanticExpression expression, RationalFunctionProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (!HasValidRationalEnvelope(request, expression, certificate, claim) || !RationalCertificateReplay.TryCreateContext(expression, request.Variable, budget, out RationalAnalysisContext context) || !context.Function.Numerator.Equals(certificate.Numerator) || !context.Function.Denominator.Equals(certificate.Denominator) || !SamePolynomials(context.Extraction.DomainExclusions, certificate.OriginalDomainExclusions) || !RuleMatchesFeature(certificate.Feature, certificate.Rule))
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

        if (certificate.Feature == AnalysisFeatures.Range)
        {
            return RationalRangeProjection.VerifyLegacyPolynomialProof(context, certificate, claim, budget);
        }

        if (!RationalCertificateReplay.TryCompute(context, certificate.Feature, budget, out object expected, out ImmutableArray<RootIsolationCertificate> expectedRoots) || !SameRootIsolations(expectedRoots, certificate.RootIsolations))
        {
            return false;
        }

        return string.Equals(ClaimCanonical.ForObject(expected), claim, StringComparison.Ordinal);
    }

    private static bool HasValidRationalEnvelope(AnalysisRequest request, SemanticExpression expression, RationalFunctionProofCertificate certificate, string claim)
    {
        return certificate.Feature == certificate.ProvenFeature && request.Features.HasFlag(certificate.Feature) &&
               string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) &&
               string.Equals(certificate.SubjectCanonical, expression.Value.Canonical, StringComparison.Ordinal) &&
               string.Equals(certificate.Claim, claim, StringComparison.Ordinal) &&
               string.Equals(certificate.ClaimCanonical, claim, StringComparison.Ordinal);
    }

    private static bool RuleMatchesFeature(AnalysisFeatures feature, string rule)
    {
        return feature switch
        {
            AnalysisFeatures.Zeros => rule == "rational-zero-cell-decomposition",
            AnalysisFeatures.YIntercept => rule == "rational-origin-substitution",
            AnalysisFeatures.Parity => rule == "symmetric-domain-rational-identity",
            AnalysisFeatures.Range => rule == "certified-rational-range-projection",
            AnalysisFeatures.Minima or AnalysisFeatures.Maxima => rule == "derivative-sign-cell-classification",
            AnalysisFeatures.InflectionPoints => rule == "two-sided-second-derivative-sign-change",
            AnalysisFeatures.Monotonicity => rule == "maximal-domain-derivative-sign-cells",
            AnalysisFeatures.VerticalAsymptotes => rule == "reduced-denominator-poles",
            AnalysisFeatures.HorizontalAsymptotes => rule == "rational-degree-limit",
            AnalysisFeatures.ObliqueAsymptotes => rule == "rational-polynomial-division-limit",
            AnalysisFeatures.Period => rule == "nonconstant-rational-functions-have-no-real-period",
            _ => false
        };
    }

    private static bool SamePolynomials(ImmutableArray<UnivariatePolynomial> left, ImmutableArray<UnivariatePolynomial> right)
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

    private static bool SameRootIsolations(ImmutableArray<RootIsolationCertificate> expected, ImmutableArray<RootIsolationCertificate> actual)
    {
        if (expected.Length != actual.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!expected[index].Polynomial.Equals(actual[index].Polynomial) || !expected[index].Roots.Select(ExactRealCanonical.Format).SequenceEqual(actual[index].Roots.Select(ExactRealCanonical.Format), StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
