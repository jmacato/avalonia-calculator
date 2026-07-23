using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class ExactRationalExtractor
{
    public static bool TryExtract(ValueTerm term, string variable, ResourceBudget budget, out ExactRationalExtraction extraction)
    {
        var memo = new Dictionary<int, ExactRationalExtraction>();
        return TryExtractCore(term, variable, budget, memo, out extraction);
    }

    private static bool TryExtractCore(ValueTerm term, string variable, ResourceBudget budget, Dictionary<int, ExactRationalExtraction> memo, out ExactRationalExtraction extraction)
    {
        budget.Charge();
        if (memo.TryGetValue(term.Id, out extraction!))
        {
            return true;
        }

        if (ExactScalar.TryCreate(term, budget, out ExactScalar scalar))
        {
            extraction = new ExactRationalExtraction(ExactCoefficientPolynomial.Constant(scalar), ExactCoefficientPolynomial.Constant(ExactScalar.One), []);
            memo.Add(term.Id, extraction);
            return true;
        }

        if (term.Kind == ValueKind.Variable && string.Equals(term.Name, variable, StringComparison.Ordinal))
        {
            extraction = new ExactRationalExtraction(ExactCoefficientPolynomial.Variable, ExactCoefficientPolynomial.Constant(ExactScalar.One), []);
            memo.Add(term.Id, extraction);
            return true;
        }

        bool recognized = term.Kind switch
        {
            ValueKind.Negate => TryNegate(term, variable, budget, memo, out extraction),
            ValueKind.Add or ValueKind.Subtract or ValueKind.Multiply or ValueKind.Divide => TryBinary(term, variable, budget, memo, out extraction),
            ValueKind.Power => TryIntegralPower(term, variable, budget, memo, out extraction),
            _ => Fail(out extraction)
        };
        if (recognized)
        {
            memo.Add(term.Id, extraction);
        }

        return recognized;
    }

    private static bool TryNegate(ValueTerm term, string variable, ResourceBudget budget, Dictionary<int, ExactRationalExtraction> memo, out ExactRationalExtraction extraction)
    {
        if (!TryExtractCore(term.Operands[0], variable, budget, memo, out ExactRationalExtraction inner))
        {
            return Fail(out extraction);
        }

        extraction = inner with
        {
            Numerator = inner.Numerator.Negate(budget)
        };
        return true;
    }

    private static bool TryBinary(ValueTerm term, string variable, ResourceBudget budget, Dictionary<int, ExactRationalExtraction> memo, out ExactRationalExtraction extraction)
    {
        if (!TryExtractCore(term.Operands[0], variable, budget, memo, out ExactRationalExtraction left) || !TryExtractCore(term.Operands[1], variable, budget, memo, out ExactRationalExtraction right))
        {
            return Fail(out extraction);
        }

        if (!ExactCoefficientPolynomial.TryMultiply(left.Numerator, right.Denominator, budget, out ExactCoefficientPolynomial leftScaled) || !ExactCoefficientPolynomial.TryMultiply(right.Numerator, left.Denominator, budget, out ExactCoefficientPolynomial rightScaled) || !ExactCoefficientPolynomial.TryMultiply(left.Denominator, right.Denominator, budget, out ExactCoefficientPolynomial commonDenominator))
        {
            return Fail(out extraction);
        }

        ExactCoefficientPolynomial numerator;
        ExactCoefficientPolynomial denominator;
        var exclusions = ImmutableArray.CreateBuilder<ExactCoefficientPolynomial>();
        exclusions.AddRange(left.DomainExclusions);
        exclusions.AddRange(right.DomainExclusions);
        switch (term.Kind)
        {
            case ValueKind.Add:
                if (!ExactCoefficientPolynomial.TryAdd(leftScaled, rightScaled, budget, out numerator))
                {
                    return Fail(out extraction);
                }

                denominator = commonDenominator;
                break;
            case ValueKind.Subtract:
                if (!ExactCoefficientPolynomial.TrySubtract(leftScaled, rightScaled, budget, out numerator))
                {
                    return Fail(out extraction);
                }

                denominator = commonDenominator;
                break;
            case ValueKind.Multiply:
                if (!ExactCoefficientPolynomial.TryMultiply(left.Numerator, right.Numerator, budget, out numerator))
                {
                    return Fail(out extraction);
                }

                denominator = commonDenominator;
                break;
            case ValueKind.Divide:
                if (right.Numerator.IsZero || !ExactCoefficientPolynomial.TryMultiply(left.Numerator, right.Denominator, budget, out numerator) || !ExactCoefficientPolynomial.TryMultiply(left.Denominator, right.Numerator, budget, out denominator))
                {
                    return Fail(out extraction);
                }

                exclusions.Add(right.Numerator);
                break;
            default:
                return Fail(out extraction);
        }

        if (!denominator.IsConstant || !denominator[0].IsOne)
        {
            exclusions.Add(denominator);
        }

        extraction = new ExactRationalExtraction(numerator, denominator, DistinctExclusions(exclusions));
        return true;
    }

    private static bool TryIntegralPower(ValueTerm term, string variable, ResourceBudget budget, Dictionary<int, ExactRationalExtraction> memo, out ExactRationalExtraction extraction)
    {
        if (term.Operands[1].Kind != ValueKind.Constant || !term.Operands[1].Constant.IsInteger || term.Operands[1].Constant.Numerator < 0)
        {
            return Fail(out extraction);
        }

        if (term.Operands[1].Constant.Numerator > AnalysisLimits.UnivariateDegree)
        {
            throw new BudgetExceededException(nameof(AnalysisLimits.UnivariateDegree));
        }

        if (!TryExtractCore(term.Operands[0], variable, budget, memo, out ExactRationalExtraction basis))
        {
            return Fail(out extraction);
        }

        int exponent = (int)term.Operands[1].Constant.Numerator;
        ExactCoefficientPolynomial numerator = ExactCoefficientPolynomial.Constant(ExactScalar.One);
        ExactCoefficientPolynomial denominator = numerator;
        for (int i = 0; i < exponent; i++)
        {
            if (!ExactCoefficientPolynomial.TryMultiply(numerator, basis.Numerator, budget, out numerator) || !ExactCoefficientPolynomial.TryMultiply(denominator, basis.Denominator, budget, out denominator))
            {
                return Fail(out extraction);
            }
        }

        extraction = new ExactRationalExtraction(numerator, denominator, basis.DomainExclusions);
        return true;
    }

    private static ImmutableArray<ExactCoefficientPolynomial> DistinctExclusions(IEnumerable<ExactCoefficientPolynomial> exclusions)
    {
        return exclusions.Where(static exclusion => !exclusion.IsConstant || exclusion[0].IsZero)
            .DistinctBy(static exclusion => exclusion.Canonical)
            .OrderBy(static exclusion => exclusion.Canonical, StringComparer.Ordinal).ToImmutableArray();
    }

    private static bool Fail(out ExactRationalExtraction extraction)
    {
        extraction = null!;
        return false;
    }
}
