namespace Graphing.Symbolics;

internal sealed record SingleHoleSineContext(SemanticExpression Expression, SemanticExpression Sine, SemanticExpression Guard, AffineTrigPattern Pattern, DifferenceSet Domain)
{
    public string PatternCanonical => $"single-origin-hole-sine:{Pattern.Canonical}";

    public static bool TryCreate(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out SingleHoleSineContext context)
    {
        budget.Charge();
        if (expression.SourceOperands.Length != 2 || expression.RewriteHistory.Count(static rewrite => rewrite.Rule == "multiplicative-identity") != 1)
        {
            context = null!;
            return false;
        }

        SemanticExpression first = expression.SourceOperands[0];
        SemanticExpression second = expression.SourceOperands[1];
        SemanticExpression sine;
        SemanticExpression guard;
        if (IsSelfDivisionByVariable(first, variable))
        {
            guard = first;
            sine = second;
        }
        else if (IsSelfDivisionByVariable(second, variable))
        {
            guard = second;
            sine = first;
        }
        else
        {
            context = null!;
            return false;
        }

        if (expression.Value.Id != sine.Value.Id || !TrigonometricAndLatticeAnalyzer.TryGetAffineTrig(sine.Value, variable, budget, out AffineTrigPattern pattern) || pattern.Function != "sin" || pattern.Amplitude.IsZero || !pattern.Phase.IsZero || !pattern.Shift.IsZero || !TrigonometricAndLatticeAnalyzer.DomainMatches(sine, variable, angleUnit, AllRealSet.Instance, budget) || !string.Equals(expression.DefinedWhen.Canonical, guard.DefinedWhen.Canonical, StringComparison.Ordinal))
        {
            context = null!;
            return false;
        }

        var zero = new RationalReal(BigRational.Zero);
        var domain = new DifferenceSet(AllRealSet.Instance, new PointSet([zero]));
        context = new SingleHoleSineContext(expression, sine, guard, pattern, domain);
        return true;
    }

    private static bool IsSelfDivisionByVariable(SemanticExpression expression, string variable)
    {
        if (expression.Value is not { Kind: ValueKind.Constant, Constant.IsOne: true } || expression.SourceOperands is not [var numerator, var denominator] || numerator.Value.Id != denominator.Value.Id || numerator.Value is not { Kind: ValueKind.Variable, Name: var name } || !name.Equals(variable, StringComparison.OrdinalIgnoreCase) || expression.RewriteHistory.Count(static rewrite => rewrite.Rule == "self-division-value") != 1)
        {
            return false;
        }

        var zero = new ValueTerm(-1, ValueKind.Constant, BigRational.Zero, string.Empty, [], "q:0");
        Formula expected = Formula.Compare(denominator.Value, Comparison.NotEqual, zero);
        return string.Equals(expression.DefinedWhen.Canonical, expected.Canonical, StringComparison.Ordinal);
    }
}
