using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal static class MixedTrigonometricSolver
{
    public static bool TryDomain(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out RealSet domain) => TrySolveDefinedness(expression.DefinedWhen, variable, angleUnit, budget, out domain);
    public static bool TryZeros(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out RealSet zeros)
    {
        if (!TrySolveAlgebraicRestrictions(expression.DefinedWhen, variable, angleUnit, budget, out RealSet domain))
        {
            zeros = null!;
            return false;
        }

        var factors = new List<ValueTerm>();
        FlattenProduct(expression.Value, factors);
        if (factors.Count < 2)
        {
            zeros = null!;
            return false;
        }

        var factorZeros = new List<RealSet>();
        foreach (ValueTerm factor in factors)
        {
            if (!TryFactorZeros(factor, variable, angleUnit, budget, out RealSet set))
            {
                zeros = null!;
                return false;
            }

            factorZeros.Add(set);
        }

        RealSet candidates = RealSets.Union(factorZeros);
        return RealSetIntersectionSolver.TryIntersect(candidates, domain, budget, out zeros);
    }

    public static bool TryYIntercept(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out OptionalValue<ExactReal> intercept)
    {
        if (!TrySolveAlgebraicRestrictions(expression.DefinedWhen, variable, angleUnit, budget, out RealSet domain) || !RealSetIntersectionSolver.TryContainsRational(domain, BigRational.Zero, out bool containsZero))
        {
            intercept = default;
            return false;
        }

        if (!containsZero)
        {
            intercept = OptionalValue<ExactReal>.None;
            return true;
        }

        intercept = default;
        return false;
    }

    internal static bool TrySolveAlgebraicRestrictions(Formula formula, string variable, AngleUnit angleUnit, ResourceBudget budget, out RealSet domain)
    {
        if (formula is not JunctionFormula { IsConjunction: true } conjunction)
        {
            domain = null!;
            return false;
        }

        var algebraic = new List<Formula>();
        bool removedPeriodic = false;
        foreach (Formula operand in conjunction.Operands)
        {
            if (TryPeriodicAtom(operand, variable, angleUnit, budget, out _))
            {
                removedPeriodic = true;
            }
            else
            {
                algebraic.Add(operand);
            }
        }

        if (!removedPeriodic || !PolynomialFormulaConverter.TryConvert(Formula.And(algebraic), variable, budget, out PolynomialFormula polynomialFormula))
        {
            domain = null!;
            return false;
        }

        domain = CellDecomposer.Decompose(polynomialFormula, budget).Result;
        return true;
    }

    private static bool TrySolveDefinedness(Formula formula, string variable, AngleUnit angleUnit, ResourceBudget budget, out RealSet domain)
    {
        if (TryPeriodicAtom(formula, variable, angleUnit, budget, out domain))
        {
            return true;
        }

        if (formula is not JunctionFormula { IsConjunction: true } conjunction)
        {
            domain = null!;
            return false;
        }

        var algebraic = new List<Formula>();
        var periodic = new List<RealSet>();
        foreach (Formula operand in conjunction.Operands)
        {
            if (TryPeriodicAtom(operand, variable, angleUnit, budget, out RealSet periodicSet))
            {
                periodic.Add(periodicSet);
            }
            else
            {
                algebraic.Add(operand);
            }
        }

        if (periodic.Count == 0 || !PolynomialFormulaConverter.TryConvert(Formula.And(algebraic), variable, budget, out PolynomialFormula polynomialFormula))
        {
            domain = null!;
            return false;
        }

        RealSet result = CellDecomposer.Decompose(polynomialFormula, budget).Result;
        foreach (RealSet periodicSet in periodic)
        {
            if (!RealSetIntersectionSolver.TryIntersect(result, periodicSet, budget, out result))
            {
                domain = null!;
                return false;
            }
        }

        domain = result;
        return true;
    }

    private static bool TryPeriodicAtom(Formula formula, string variable, AngleUnit angleUnit, ResourceBudget budget, out RealSet set)
    {
        if (formula is ComparisonFormula { Comparison: Comparison.NotEqual, Left: var primitive, Right: { Kind: ValueKind.Constant, Constant.IsZero: true } } && TrigonometricAndLatticeAnalyzer.TryGetAffineTrig(primitive, variable, budget, out AffineTrigPattern pattern) && pattern.Function is "sin" or "cos" && pattern.Shift.IsZero)
        {
            set = AffineReciprocalTrigonometricAnalyzer.NonzeroDomain(pattern, angleUnit, budget);
            return true;
        }

        set = null!;
        return false;
    }

    internal static bool TryFactorZeros(ValueTerm factor, string variable, AngleUnit angleUnit, ResourceBudget budget, out RealSet zeros)
    {
        if (TrigonometricAndLatticeAnalyzer.TryGetAffineTrig(factor, variable, budget, out AffineTrigPattern trig))
        {
            zeros = TrigonometricAndLatticeAnalyzer.AffineZeros(trig, angleUnit);
            return true;
        }

        if (factor.Kind == ValueKind.Function && factor.Name is "log" or "ln" && factor.Operands.Length == 1 && RationalFunctionExtractor.TryExtract(factor.Operands[0], variable, budget, out RationalExtraction argument))
        {
            RationalFunction difference = argument.Function.Subtract(RationalFunction.Constant(BigRational.One, budget), budget);
            if (difference.Numerator.Degree == 1)
            {
                BigRational root = -difference.Numerator[0] / difference.Numerator[1];
                zeros = RealSets.Points([new RationalReal(root)]);
                return true;
            }
        }

        zeros = null!;
        return false;
    }

    private static void FlattenProduct(ValueTerm term, List<ValueTerm> factors)
    {
        if (term.Kind == ValueKind.Multiply)
        {
            FlattenProduct(term.Operands[0], factors);
            FlattenProduct(term.Operands[1], factors);
            return;
        }

        factors.Add(term);
    }
}
