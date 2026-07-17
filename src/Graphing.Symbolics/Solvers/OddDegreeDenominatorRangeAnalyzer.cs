namespace Graphing.Symbolics;

/// <summary>
/// Proves the image of c / D(x), where c is a nonzero rational constant and
/// D has positive odd degree, on precisely the domain D(x) != 0.
/// </summary>
internal static class OddDegreeDenominatorRangeAnalyzer
{
    internal const string Rule = "odd-degree-denominator-surjectivity";

    public static bool TryAnalyze(
        RationalAnalysisContext context,
        ResourceBudget budget,
        out ProofOutcome<RealSet> outcome)
    {
        budget.Charge();
        RationalFunction function = context.Function;
        if (!HasTheoremShape(function))
        {
            outcome = null!;
            return false;
        }

        CellDecompositionCertificate denominatorDomain = DecomposeDenominatorDomain(
            function.Denominator,
            budget);
        if (!string.Equals(
                denominatorDomain.Result.Canonical,
                context.Domain.Canonical,
                StringComparison.Ordinal))
        {
            outcome = null!;
            return false;
        }

        RealSet range = NonzeroReals();
        var certificate = new OddDegreeDenominatorRangeProofCertificate(
            AnalysisFeatures.Range,
            context.Expression.Value.Canonical,
            ClaimCanonical.For(range),
            function.Numerator,
            function.Denominator,
            context.Expression.DefinedWhen.Canonical,
            context.DomainFormula,
            context.DomainCells,
            denominatorDomain,
            Rule);
        outcome = ProofOutcome<RealSet>.Proved(range, certificate);
        return true;
    }

    private static bool HasTheoremShape(RationalFunction function) =>
        function.Numerator.IsConstant &&
        !function.Numerator.ConstantCoefficient.IsZero &&
        function.Denominator.Degree > 0 &&
        (function.Denominator.Degree & 1) != 0;

    private static CellDecompositionCertificate DecomposeDenominatorDomain(
        UnivariatePolynomial denominator,
        ResourceBudget budget)
    {
        var nonzero = new PolynomialAtom(
            denominator.PrimitivePositive(budget),
            Comparison.NotEqual);
        return CellDecomposer.Decompose(nonzero, budget);
    }

    private static Graphing.Symbolics.DifferenceSet NonzeroReals() => new DifferenceSet(
        AllRealSet.Instance,
        RealSets.Points([new RationalReal(BigRational.Zero)]));
}
