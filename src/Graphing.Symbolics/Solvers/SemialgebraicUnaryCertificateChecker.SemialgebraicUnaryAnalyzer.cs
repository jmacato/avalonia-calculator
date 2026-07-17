using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class SemialgebraicUnaryCertificateChecker
{
    public static bool Check(AnalysisRequest request, SemanticExpression expression, SemialgebraicUnaryProofCertificate certificate, string claim, ResourceBudget budget)
    {
        budget.Charge();
        if (!SemialgebraicUnaryAnalyzer.IsRule(certificate.Rule) || certificate.Feature != certificate.ProvenFeature || !request.Features.HasFlag(certificate.Feature) || !string.Equals(certificate.Subject, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.SubjectCanonical, expression.Value.Canonical, StringComparison.Ordinal) || !string.Equals(certificate.Claim, claim, StringComparison.Ordinal) || !string.Equals(certificate.ClaimCanonical, claim, StringComparison.Ordinal) || !TryCreateReplayContext(expression, request.Variable, budget, out SemialgebraicUnaryContext context) || context.Kind != certificate.Kind || !context.Inner.Numerator.Equals(certificate.InnerNumerator) || !context.Inner.Denominator.Equals(certificate.InnerDenominator) || !string.Equals(context.DomainFormula.Canonical, certificate.DomainFormula.Canonical, StringComparison.Ordinal) || !SemialgebraicUnaryCertificateReplay.VerifyChart(context, certificate.Chart, budget))
        {
            return false;
        }

        ImmutableArray<CellDecompositionCertificate> expectedAuxiliaries = certificate.Feature == AnalysisFeatures.Parity ? SemialgebraicUnaryCertificateReplay.BuildParityCells(context, budget) : [];
        if (!VerifyAuxiliaries(expectedAuxiliaries, certificate.AuxiliaryCells, budget))
        {
            return false;
        }

        object value;
        if (certificate.Feature == AnalysisFeatures.Range)
        {
            if (!SemialgebraicUnaryCertificateReplay.VerifyRangeProof(context, certificate.Chart, certificate.RangeBoundaries, certificate.RangeFibers, budget, out RealSet range))
            {
                return false;
            }

            value = range;
        }
        else
        {
            if (!certificate.RangeBoundaries.IsEmpty || !certificate.RangeFibers.IsEmpty || !SemialgebraicUnaryCertificateReplay.TryComputeFeature(context, certificate.Chart, certificate.Feature, certificate.AuxiliaryCells, budget, out value))
            {
                return false;
            }
        }

        return string.Equals(ClaimCanonical.ForObject(value), claim, StringComparison.Ordinal);
    }

    private static bool TryCreateReplayContext(SemanticExpression expression, string variable, ResourceBudget budget, out SemialgebraicUnaryContext context)
    {
        budget.Charge();
        if (!TryMatchSource(expression, budget, out SemialgebraicUnaryKind kind, out ValueTerm core) || !RationalFunctionExtractor.TryExtract(core, variable, budget, out RationalExtraction extraction) || !PolynomialFormulaConverter.TryConvert(expression.DefinedWhen, variable, budget, out PolynomialFormula domainFormula))
        {
            context = null!;
            return false;
        }

        CellDecompositionCertificate domainCells = CellDecomposer.Decompose(domainFormula, budget);
        if (extraction.Function.Numerator.IsConstant && extraction.Function.Denominator.IsConstant && domainCells.Result is AllRealSet)
        {
            context = null!;
            return false;
        }

        context = new SemialgebraicUnaryContext(expression, variable, kind, extraction.Function, domainFormula, domainCells);
        return true;
    }

    private static bool TryMatchSource(SemanticExpression expression, ResourceBudget budget, out SemialgebraicUnaryKind kind, out ValueTerm core)
    {
        if (expression.Value is { Kind: ValueKind.Function, Name: "abs", Operands: [var operand] } && TryMatchAbsoluteSource(expression, operand, budget, out core))
        {
            kind = SemialgebraicUnaryKind.AbsoluteValue;
            return true;
        }

        if (expression.Value is { Kind: ValueKind.Function, Name: "sqrt", Operands: [var radicand] } && expression.SourceOperands is [var sourceRadicand] && sourceRadicand.Value.Id == radicand.Id)
        {
            kind = SemialgebraicUnaryKind.PrincipalSquareRoot;
            core = radicand;
            return true;
        }

        kind = default;
        core = null!;
        return false;
    }

    private static bool TryMatchAbsoluteSource(SemanticExpression expression, ValueTerm operand, ResourceBudget budget, out ValueTerm core)
    {
        budget.Charge();
        if (expression.SourceOperands is [var source] && source.Value.Id == operand.Id)
        {
            if (operand is { Kind: ValueKind.Function, Name: "abs", Operands: [var nested] })
            {
                return TryMatchAbsoluteSource(source, nested, budget, out core);
            }

            core = operand;
            return true;
        }

        if (expression.SourceOperands is not [var square] || expression.RewriteHistory is not [{ Rule: "sqrt-square-to-absolute-value" }] || square.Value is not { Kind: ValueKind.Power, Operands: [var basisValue, var exponentValue] } || basisValue.Id != operand.Id || square.SourceOperands is not [var sourceBasis, var sourceExponent] || sourceBasis.Value.Id != basisValue.Id || sourceExponent.Value.Id != exponentValue.Id || !ExactScalar.TryCreate(exponentValue, budget, out ExactScalar exponent) || exponent.RationalValue != new BigRational(2))
        {
            core = null!;
            return false;
        }

        core = operand;
        return true;
    }

    private static bool VerifyAuxiliaries(ImmutableArray<CellDecompositionCertificate> expected, ImmutableArray<CellDecompositionCertificate> actual, ResourceBudget budget)
    {
        if (expected.Length != actual.Length)
        {
            return false;
        }

        for (int index = 0; index < expected.Length; index++)
        {
            if (!string.Equals(expected[index].Formula.Canonical, actual[index].Formula.Canonical, StringComparison.Ordinal) || !CellDecomposer.Verify(actual[index], budget) || !string.Equals(expected[index].Result.Canonical, actual[index].Result.Canonical, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
