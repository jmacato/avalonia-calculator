using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class RationalFunctionExtractor
{
    public static bool TryExtract(ValueTerm term, string variable, ResourceBudget budget, out RationalExtraction extraction)
    {
        var exclusions = ImmutableArray.CreateBuilder<UnivariatePolynomial>();
        if (!TryExtractCore(term, variable, exclusions, budget, out RationalFunction? function))
        {
            extraction = null!;
            return false;
        }

        extraction = new RationalExtraction(function, exclusions.Where(static polynomial => !polynomial.IsConstant).DistinctBy(static polynomial => polynomial.Canonical).OrderBy(static polynomial => polynomial.Canonical, StringComparer.Ordinal).ToImmutableArray());
        return true;
    }

    private static bool TryExtractCore(ValueTerm term, string variable, ImmutableArray<UnivariatePolynomial>.Builder exclusions, ResourceBudget budget, out RationalFunction function)
    {
        budget.Charge();
        switch (term.Kind)
        {
            case ValueKind.Constant:
                function = RationalFunction.Constant(term.Constant, budget);
                return true;
            case ValueKind.Variable when term.Name.Equals(variable, StringComparison.OrdinalIgnoreCase):
                function = RationalFunction.Variable;
                return true;
            case ValueKind.Negate:
                if (TryExtractCore(term.Operands[0], variable, exclusions, budget, out RationalFunction negated))
                {
                    function = negated.Negate(budget);
                    return true;
                }

                break;
            case ValueKind.Add:
            case ValueKind.Subtract:
            case ValueKind.Multiply:
            case ValueKind.Divide:
                if (TryExtractCore(term.Operands[0], variable, exclusions, budget, out RationalFunction left) && TryExtractCore(term.Operands[1], variable, exclusions, budget, out RationalFunction right))
                {
                    if (term.Kind == ValueKind.Divide)
                    {
                        exclusions.Add(right.Numerator);
                    }

                    function = term.Kind switch
                    {
                        ValueKind.Add => left.Add(right, budget),
                        ValueKind.Subtract => left.Subtract(right, budget),
                        ValueKind.Multiply => left.Multiply(right, budget),
                        ValueKind.Divide when !right.Numerator.IsZero => left.Divide(right, budget),
                        _ => null!
                    };
                    return function is not null;
                }

                break;
            case ValueKind.Power when term.Operands[1].Kind == ValueKind.Constant && term.Operands[1].Constant.IsInteger && term.Operands[1].Constant.Numerator >= int.MinValue && term.Operands[1].Constant.Numerator <= int.MaxValue:
                if (TryExtractCore(term.Operands[0], variable, exclusions, budget, out RationalFunction basis))
                {
                    int exponent = (int)term.Operands[1].Constant.Numerator;
                    if (exponent <= 0)
                    {
                        exclusions.Add(basis.Numerator);
                    }

                    function = basis.Pow(exponent, budget);
                    return true;
                }

                break;
        }

        function = null!;
        return false;
    }
}
