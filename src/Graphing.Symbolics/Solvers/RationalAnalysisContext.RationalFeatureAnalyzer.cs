using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record RationalAnalysisContext(SemanticExpression Expression, string Variable, RationalExtraction Extraction, PolynomialFormula DomainFormula, CellDecompositionCertificate DomainCells)
{
    public RationalFunction Function => Extraction.Function;
    public RealSet Domain => DomainCells.Result;

    public static bool TryCreate(SemanticExpression expression, string variable, ResourceBudget budget, out RationalAnalysisContext context)
    {
        if (!RationalFunctionExtractor.TryExtract(expression.Value, variable, budget, out RationalExtraction extraction) || !PolynomialFormulaConverter.TryConvert(expression.DefinedWhen, variable, budget, out PolynomialFormula domainFormula))
        {
            context = null!;
            return false;
        }

        CellDecompositionCertificate domainCells = CellDecomposer.Decompose(domainFormula, budget);
        context = new RationalAnalysisContext(expression, variable, extraction, domainFormula, domainCells);
        return true;
    }
}
