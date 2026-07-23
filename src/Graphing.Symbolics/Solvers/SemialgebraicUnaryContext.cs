namespace Graphing.Symbolics;

internal sealed record SemialgebraicUnaryContext(SemanticExpression Expression, string Variable, SemialgebraicUnaryKind Kind, RationalFunction Inner, PolynomialFormula DomainFormula, CellDecompositionCertificate DomainCells)
{
    public static bool TryCreate(SemanticExpression expression, string variable, ResourceBudget budget, out SemialgebraicUnaryContext context)
    {
        if (!TryMatch(expression.Value, out SemialgebraicUnaryKind kind, out ValueTerm core) || !RationalFunctionExtractor.TryExtract(core, variable, budget, out RationalExtraction extraction) || !PolynomialFormulaConverter.TryConvert(expression.DefinedWhen, variable, budget, out PolynomialFormula domainFormula))
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

    private static bool TryMatch(ValueTerm term, out SemialgebraicUnaryKind kind, out ValueTerm core)
    {
        if (term is { Kind: ValueKind.Function, Name: "abs", Operands.Length: 1 })
        {
            kind = SemialgebraicUnaryKind.AbsoluteValue;
            core = term.Operands[0];
            while (core is { Kind: ValueKind.Function, Name: "abs", Operands.Length: 1 })
            {
                core = core.Operands[0];
            }

            return true;
        }

        if (term is { Kind: ValueKind.Function, Name: "sqrt", Operands.Length: 1 })
        {
            kind = SemialgebraicUnaryKind.PrincipalSquareRoot;
            core = term.Operands[0];
            return true;
        }

        kind = default;
        core = null!;
        return false;
    }
}
