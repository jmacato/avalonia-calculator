namespace Graphing.Symbolics;

internal static class DomainSolver
{
    public static bool TrySolve(
        SemanticExpression expression,
        string variable,
        ResourceBudget budget,
        out RealSet domain,
        out DomainProofCertificate certificate)
    {
        if (!PolynomialFormulaConverter.TryConvert(
                expression.DefinedWhen,
                variable,
                budget,
                out PolynomialFormula formula))
        {
            domain = null!;
            certificate = null!;
            return false;
        }

        CellDecompositionCertificate cells = CellDecomposer.Decompose(formula, budget);
        domain = cells.Result;
        certificate = new DomainProofCertificate(
            expression.Value.Canonical,
            domain.Canonical,
            expression.DefinedWhen.Canonical,
            cells,
            "univariate-semialgebraic-definedness");
        return true;
    }
}
