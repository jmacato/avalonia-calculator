using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class PolynomialFormulaConverter
{
    public static bool TryConvert(Formula formula, string variable, ResourceBudget budget, out PolynomialFormula result)
    {
        budget.Charge();
        switch (formula)
        {
            case BooleanFormula boolean:
                result = new PolynomialBoolean(boolean.Value);
                return true;
            case ComparisonFormula comparison:
                if (!RationalFunctionExtractor.TryExtract(comparison.Left, variable, budget, out RationalExtraction left) || !RationalFunctionExtractor.TryExtract(comparison.Right, variable, budget, out RationalExtraction right))
                {
                    break;
                }

                RationalFunction difference = left.Function.Subtract(right.Function, budget);
                UnivariatePolynomial signPolynomial = difference.Numerator.Multiply(difference.Denominator, budget);
                if (signPolynomial.IsZero)
                {
                    result = new PolynomialBoolean(CompareSign(0, comparison.Comparison));
                    return true;
                }

                Comparison normalizedComparison = signPolynomial.LeadingCoefficient.Sign < 0 ? Reverse(comparison.Comparison) : comparison.Comparison;
                result = new PolynomialAtom(signPolynomial.PrimitivePositive(budget), normalizedComparison);
                return true;
            case PredicateFormula predicate when predicate.Terms.Length == 1 && ExactScalar.TryCreate(predicate.Terms[0], budget, out ExactScalar predicateScalar) && predicateScalar.RationalValue is { } value:
                result = predicate.Kind switch
                {
                    ExactPredicate.IsInteger => new PolynomialBoolean(value.IsInteger),
                    ExactPredicate.IsOddInteger => new PolynomialBoolean(value.IsInteger && !value.Numerator.IsEven),
                    _ => null!
                };
                return result is not null;
            case NotFormula not:
                if (TryConvert(not.Operand, variable, budget, out PolynomialFormula convertedNot))
                {
                    result = new PolynomialNot(convertedNot);
                    return true;
                }

                break;
            case JunctionFormula junction:
                var operands = ImmutableArray.CreateBuilder<PolynomialFormula>(junction.Operands.Length);
                foreach (Formula operand in junction.Operands)
                {
                    if (!TryConvert(operand, variable, budget, out PolynomialFormula converted))
                    {
                        result = null!;
                        return false;
                    }

                    operands.Add(converted);
                }

                result = new PolynomialJunction(junction.IsConjunction, operands.MoveToImmutable());
                return true;
        }

        result = null!;
        return false;
    }

    public static bool Evaluate(PolynomialFormula formula, IReadOnlyDictionary<string, int> signs) => formula switch
    {
        PolynomialBoolean boolean => boolean.Value,
        PolynomialAtom atom => CompareSign(signs[atom.Polynomial.Canonical], atom.Comparison),
        PolynomialNot not => !Evaluate(not.Operand, signs),
        PolynomialJunction { IsConjunction: true } conjunction => conjunction.Operands.All(operand => Evaluate(operand, signs)),
        PolynomialJunction disjunction => disjunction.Operands.Any(operand => Evaluate(operand, signs)),
        _ => throw new ArgumentOutOfRangeException(nameof(formula))
    };
    public static ImmutableArray<UnivariatePolynomial> Atoms(PolynomialFormula formula) => formula switch
    {
        PolynomialAtom atom => [atom.Polynomial],
        PolynomialNot not => Atoms(not.Operand),
        PolynomialJunction junction => junction.Operands.SelectMany(static operand => Atoms(operand)).DistinctBy(static polynomial => polynomial.Canonical).OrderBy(static polynomial => polynomial.Canonical, StringComparer.Ordinal).ToImmutableArray(),
        _ => []
    };
    private static bool CompareSign(int sign, Comparison comparison) => comparison switch
    {
        Comparison.Equal => sign == 0,
        Comparison.NotEqual => sign != 0,
        Comparison.Less => sign < 0,
        Comparison.LessOrEqual => sign <= 0,
        Comparison.Greater => sign > 0,
        Comparison.GreaterOrEqual => sign >= 0,
        _ => throw new ArgumentOutOfRangeException(nameof(comparison))
    };
    private static Comparison Reverse(Comparison comparison) => comparison switch
    {
        Comparison.Less => Comparison.Greater,
        Comparison.LessOrEqual => Comparison.GreaterOrEqual,
        Comparison.Greater => Comparison.Less,
        Comparison.GreaterOrEqual => Comparison.LessOrEqual,
        _ => comparison
    };
}
