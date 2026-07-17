namespace Graphing.Symbolics;

internal sealed record FixedRationalPowerContext(SemanticExpression Expression, string Variable, BigRational Exponent, RationalFunction Basis, PolynomialFormula DomainFormula)
{
    public static bool TryCreate(SemanticExpression expression, string variable, ResourceBudget budget, out FixedRationalPowerContext context)
    {
        budget.Charge();
        if (!TryMatch(expression.Value, budget, out ValueTerm basisTerm, out BigRational exponent))
        {
            context = null!;
            return false;
        }

        CheckExponentLimit(exponent);
        if (!RationalFunctionExtractor.TryExtract(basisTerm, variable, budget, out RationalExtraction extraction) || !PolynomialFormulaConverter.TryConvert(expression.DefinedWhen, variable, budget, out PolynomialFormula domainFormula))
        {
            context = null!;
            return false;
        }

        context = new FixedRationalPowerContext(expression, variable, exponent, extraction.Function, domainFormula);
        return true;
    }

    private static bool TryMatch(ValueTerm term, ResourceBudget budget, out ValueTerm basis, out BigRational exponent)
    {
        if (term is { Kind: ValueKind.Power, Operands.Length: 2 } power && ExactScalar.TryCreate(power.Operands[1], budget, out ExactScalar exponentScalar) && exponentScalar.RationalValue is { IsInteger: false } rationalExponent)
        {
            basis = power.Operands[0];
            exponent = rationalExponent;
            return true;
        }

        if (term is { Kind: ValueKind.Function, Name: "root", Operands.Length: 2 } root && ExactScalar.TryCreate(root.Operands[1], budget, out ExactScalar degreeScalar) && degreeScalar.RationalValue is { IsInteger: true, IsZero: false } degree && degree.Numerator.IsEven)
        {
            basis = root.Operands[0];
            exponent = degree.Reciprocal();
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
}
