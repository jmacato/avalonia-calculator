namespace Graphing.Symbolics;

internal static class ExactDefinednessVerifier
{
    public static bool MatchesRational(
        Formula definedWhen,
        ExactRationalPattern pattern,
        string variable,
        ResourceBudget budget)
    {
        if (pattern.DomainExclusions.IsEmpty)
        {
            return definedWhen is BooleanFormula { Value: true };
        }

        var actual = new List<ExactCoefficientPolynomial>();
        if (!CollectNonzeroPolynomials(definedWhen, variable, budget, actual))
        {
            return false;
        }

        ExactCoefficientPolynomial[] normalized = actual
            .Select(polynomial => Normalize(polynomial, budget))
            .DistinctBy(static polynomial => polynomial.Canonical)
            .OrderBy(static polynomial => polynomial.Canonical, StringComparer.Ordinal)
            .ToArray();
        return normalized.Length == pattern.DomainExclusions.Length &&
               normalized.Zip(pattern.DomainExclusions)
                   .All(static pair => pair.First.EqualsPolynomial(pair.Second));
    }

    public static bool MatchesTrig(
        Formula definedWhen,
        ExactTrigPattern pattern,
        string variable,
        ResourceBudget budget)
    {
        if (pattern.Kind != ExactCoefficientPatternKind.AffineTangent)
        {
            return definedWhen is BooleanFormula { Value: true };
        }

        ValueTerm? argument = null;
        if (!ExactFormulaVerifier.MatchesSingleGuard(
                definedWhen,
                CaptureTangentArgument,
                budget) ||
            argument is null ||
            !ExactRationalExtractor.TryExtract(
                argument,
                variable,
                budget,
                out ExactRationalExtraction extraction) ||
            !extraction.DomainExclusions.IsEmpty ||
            !extraction.Denominator.IsConstant ||
            extraction.Numerator.Degree != 1)
        {
            return false;
        }

        ExactScalar reciprocal = extraction.Denominator[0].Reciprocal(budget);
        ExactScalar frequency = extraction.Numerator[1].Multiply(reciprocal, budget);
        ExactScalar phase = extraction.Numerator[0].Multiply(reciprocal, budget);
        return string.Equals(frequency.Abs().Canonical, pattern.Frequency.Canonical, StringComparison.Ordinal) &&
               (frequency.Sign > 0
                   ? string.Equals(phase.Canonical, pattern.Phase.Canonical, StringComparison.Ordinal)
                   : string.Equals(phase.Negate().Canonical, pattern.Phase.Canonical, StringComparison.Ordinal));

        bool CaptureTangentArgument(Formula formula)
        {
            if (!TryGetTangentArgument(formula, out ValueTerm candidate))
            {
                return false;
            }

            argument = candidate;
            return true;
        }
    }

    private static bool TryGetTangentArgument(Formula formula, out ValueTerm argument)
    {
        if (formula is ComparisonFormula
            {
                Comparison: Comparison.NotEqual,
                Left:
                {
                    Kind: ValueKind.Function,
                    Name: "cos",
                    Operands: [ValueTerm candidate]
                },
                Right:
                {
                    Kind: ValueKind.Constant,
                    Constant.IsZero: true
                }
            })
        {
            argument = candidate;
            return true;
        }

        argument = null!;
        return false;
    }

    private static bool CollectNonzeroPolynomials(
        Formula formula,
        string variable,
        ResourceBudget budget,
        List<ExactCoefficientPolynomial> polynomials)
    {
        budget.Charge();
        if (ExactFormulaVerifier.IsAlwaysTrue(formula, budget))
        {
            return true;
        }

        if (formula is JunctionFormula { IsConjunction: true } conjunction)
        {
            return conjunction.Operands.All(operand =>
                CollectNonzeroPolynomials(operand, variable, budget, polynomials));
        }

        if (formula is not ComparisonFormula
            {
                Comparison: Comparison.NotEqual
            } comparison)
        {
            return false;
        }

        ValueTerm candidate;
        if (comparison.Right.Kind == ValueKind.Constant && comparison.Right.Constant.IsZero)
        {
            candidate = comparison.Left;
        }
        else if (comparison.Left.Kind == ValueKind.Constant && comparison.Left.Constant.IsZero)
        {
            candidate = comparison.Right;
        }
        else
        {
            return false;
        }

        if (!ExactRationalExtractor.TryExtract(
                candidate,
                variable,
                budget,
                out ExactRationalExtraction extraction) ||
            !extraction.DomainExclusions.IsEmpty ||
            !extraction.Denominator.IsConstant)
        {
            return false;
        }

        polynomials.Add(extraction.Numerator.Scale(
            extraction.Denominator[0].Reciprocal(budget),
            budget));
        return true;
    }

    private static ExactCoefficientPolynomial Normalize(
        ExactCoefficientPolynomial polynomial,
        ResourceBudget budget)
    {
        return polynomial.IsZero || polynomial[polynomial.Degree].IsOne
            ? polynomial
            : polynomial.Scale(polynomial[polynomial.Degree].Reciprocal(budget), budget);
    }
}
