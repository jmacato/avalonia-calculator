using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class FixedRationalPowerCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, FixedRationalPowerProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (!FixedRationalPowerAnalyzer.IsRule(certificate.Rule) || certificate.Feature != certificate.ProvenFeature || !request.Features.HasFlag(certificate.Feature) || !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.SubjectCanonical, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || !string.Equals(certificate.ClaimCanonical, claim, StringComparison.Ordinal) || !TryCreateReplayContext(expression, request.Variable, budget, out FixedRationalPowerContext context) || context.Exponent != certificate.Exponent || !context.Basis.Numerator.Equals(certificate.BaseNumerator) || !context.Basis.Denominator.Equals(certificate.BaseDenominator) || !string.Equals(context.DomainFormula.Canonical, certificate.DomainFormula.Canonical, StringComparison.Ordinal) || !FixedRationalPowerCertificateReplay.VerifySignChart(context, certificate.SignChart, budget))
        {
            return false;
        }

        ImmutableArray<CellDecompositionCertificate> expectedAuxiliaries = certificate.Feature == AnalysisFeatures.Parity ? FixedRationalPowerCertificateReplay.BuildParityCells(context, budget) : [];
        if (!VerifyAuxiliaries(expectedAuxiliaries, certificate.AuxiliaryCells, budget))
        {
            return false;
        }

        object value;
        if (certificate.Feature == AnalysisFeatures.Range)
        {
            if (!FixedRationalPowerCertificateReplay.VerifyRangeProof(context, certificate.SignChart, certificate.RangeBoundaries, certificate.RangeFibers, budget, out RealSet range))
            {
                return false;
            }

            value = range;
        }
        else
        {
            if (!certificate.RangeBoundaries.IsEmpty || !certificate.RangeFibers.IsEmpty || !FixedRationalPowerCertificateReplay.TryComputeFeature(context, certificate.SignChart, certificate.Feature, certificate.AuxiliaryCells, budget, out value))
            {
                return false;
            }
        }

        return string.Equals(ClaimCanonical.ForObject(value), claim, StringComparison.Ordinal);
    }

    private static bool TryCreateReplayContext(SemanticExpression expression, string variable, ResourceBudget budget, out FixedRationalPowerContext context)
    {
        budget.Charge();
        if (!TryMatchSource(expression, budget, out SemanticExpression basis, out BigRational exponent) || !RationalFunctionExtractor.TryExtract(basis.Value, variable, budget, out RationalExtraction extraction) || !PolynomialFormulaConverter.TryConvert(expression.DefinedWhen, variable, budget, out PolynomialFormula domainFormula))
        {
            context = null!;
            return false;
        }

        CheckExponentLimit(exponent);
        context = new FixedRationalPowerContext(expression, variable, exponent, extraction.Function, domainFormula);
        return true;
    }

    private static bool TryMatchSource(SemanticExpression expression, ResourceBudget budget, out SemanticExpression basis, out BigRational exponent)
    {
        if (expression.Value is { Kind: ValueKind.Power, Operands: [var basisValue, var exponentValue] } && expression.SourceOperands is [var sourceBasis, var sourceExponent] && sourceBasis.Value.Id == basisValue.Id && sourceExponent.Value.Id == exponentValue.Id && ExactScalar.TryCreate(sourceExponent.Value, budget, out ExactScalar exponentScalar) && exponentScalar.RationalValue is { IsInteger: false } rationalExponent)
        {
            basis = sourceBasis;
            exponent = rationalExponent;
            return true;
        }

        if (expression.Value is { Kind: ValueKind.Function, Name: "root", Operands: [var radicandValue, var degreeValue] } && expression.SourceOperands is [var radicand, var degree] && radicand.Value.Id == radicandValue.Id && degree.Value.Id == degreeValue.Id && ExactScalar.TryCreate(degree.Value, budget, out ExactScalar degreeScalar) && degreeScalar.RationalValue is { IsInteger: true, IsZero: false } rationalDegree && rationalDegree.Numerator.IsEven)
        {
            basis = radicand;
            exponent = rationalDegree.Reciprocal();
            return true;
        }

        basis = null!;
        exponent = default;
        return false;
    }

    private static void CheckExponentLimit(BigRational exponent)
    {
        ExactInteger magnitude = ExactInteger.Abs(exponent.Numerator);
        if (magnitude > AnalysisLimits.UnivariateDegree || exponent.Denominator > AnalysisLimits.UnivariateDegree)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.UnivariateDegree));
        }
    }

    private static bool VerifyAuxiliaries(ImmutableArray<CellDecompositionCertificate> expected, ImmutableArray<CellDecompositionCertificate> actual, ResourceBudget budget)
    {
        if (expected.Length != actual.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            budget.Charge();
            if (!string.Equals(expected[index].Formula.Canonical, actual[index].Formula.Canonical, StringComparison.Ordinal) || !CellDecomposer.Verify(actual[index], budget) || !string.Equals(expected[index].Result.Canonical, actual[index].Result.Canonical, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
