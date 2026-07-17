namespace Graphing.Symbolics;

/// <summary>
/// Independent production replay of the odd-degree surjectivity theorem.
/// It reconstructs the reduced rational function and the exact denominator
/// domain from the semantic expression rather than trusting solver control
/// flow or any sampled value.
/// </summary>
internal static class OddDegreeDenominatorRangeCertificateChecker
{
    public static bool Check(
        AnalysisRequest request,
        SemanticExpression expression,
        OddDegreeDenominatorRangeProofCertificate certificate,
        string claim,
        ResourceBudget budget)
    {
        budget.Charge();
        if (certificate.Feature != AnalysisFeatures.Range ||
            certificate.ProvenFeature != AnalysisFeatures.Range ||
            !string.Equals(certificate.Rule, OddDegreeDenominatorRangeAnalyzer.Rule, StringComparison.Ordinal) ||
            !string.Equals(certificate.Subject, certificate.SubjectCanonical, StringComparison.Ordinal) ||
            !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) ||
            !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) ||
            !string.Equals(
                certificate.DefinednessCanonical,
                expression.DefinedWhen.Canonical,
                StringComparison.Ordinal) ||
            !RationalFunctionExtractor.TryExtract(
                expression.Value,
                request.Variable,
                budget,
                out RationalExtraction extraction) ||
            !PolynomialFormulaConverter.TryConvert(
                expression.DefinedWhen,
                request.Variable,
                budget,
                out PolynomialFormula sourceDomainFormula))
        {
            return false;
        }

        RationalFunction function = extraction.Function;
        if (!function.Numerator.Equals(certificate.Numerator) ||
            !function.Denominator.Equals(certificate.Denominator) ||
            !function.Numerator.IsConstant ||
            function.Numerator.ConstantCoefficient.IsZero ||
            function.Denominator.Degree <= 0 ||
            (function.Denominator.Degree & 1) == 0)
        {
            return false;
        }

        var expectedFormula = new PolynomialAtom(
            function.Denominator.PrimitivePositive(budget),
            Comparison.NotEqual);
        if (!string.Equals(
                certificate.DomainFormula.Canonical,
                sourceDomainFormula.Canonical,
                StringComparison.Ordinal) ||
            !string.Equals(
                certificate.DomainCells.Formula.Canonical,
                certificate.DomainFormula.Canonical,
                StringComparison.Ordinal) ||
            !CellDecomposer.Verify(certificate.DomainCells, budget) ||
            !string.Equals(
                certificate.DenominatorDomain.Formula.Canonical,
                expectedFormula.Canonical,
                StringComparison.Ordinal) ||
            !CellDecomposer.Verify(certificate.DenominatorDomain, budget) ||
            !string.Equals(
                sourceDomainFormula.Canonical,
                expectedFormula.Canonical,
                StringComparison.Ordinal) ||
            !string.Equals(
                certificate.DomainCells.Result.Canonical,
                certificate.DenominatorDomain.Result.Canonical,
                StringComparison.Ordinal))
        {
            return false;
        }

        // For every y != 0, D(x) - c/y has the same positive odd degree as
        // D. Its opposite infinite-tail signs give a real root. At that root
        // D(x) = c/y != 0, so it is in the checked domain and c/D(x) = y.
        // Zero is impossible because c is nonzero and D is finite on-domain.
        RealSet expectedRange = new DifferenceSet(
            AllRealSet.Instance,
            RealSets.Points([new RationalReal(BigRational.Zero)]));
        return string.Equals(ClaimCanonical.For(expectedRange), claim, StringComparison.Ordinal);
    }
}
