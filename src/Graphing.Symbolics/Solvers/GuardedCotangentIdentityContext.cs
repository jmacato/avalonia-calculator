namespace Graphing.Symbolics;

internal sealed record GuardedCotangentIdentityContext(SemanticExpression Expression, SemanticExpression Numerator, SemanticExpression Denominator, AffineTrigPattern Tangent, ExactScalar CotangentScale, PeriodicIntervalSet Domain, ExactReal FunctionPeriod)
{
    public string PatternCanonical => $"guarded-cotangent-identity:{Tangent.Canonical}:{CotangentScale.Canonical}";

    public static bool TryCreate(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out GuardedCotangentIdentityContext context)
    {
        budget.Charge();
        if (expression.Value is not { Kind: ValueKind.Divide, Operands: [var numeratorValue, var denominatorValue] } || expression.SourceOperands is not [var numerator, var denominator] || numerator.Value.Id != numeratorValue.Id || denominator.Value.Id != denominatorValue.Id || !HalfAngleReducer.TryReduce(numerator.Value, variable, budget, out RationalFunction reducedNumerator) || !reducedNumerator.Numerator.Subtract(reducedNumerator.Denominator, budget).IsZero || !TrigonometricAndLatticeAnalyzer.DomainMatches(numerator, variable, angleUnit, AllRealSet.Instance, budget) || !TrigonometricAndLatticeAnalyzer.TryGetAffineTrig(denominator.Value, variable, budget, out AffineTrigPattern tangent) || tangent.Function != "tan" || tangent.Amplitude.IsZero || !tangent.Phase.IsZero || !tangent.Shift.IsZero || !TrigonometricAndLatticeAnalyzer.DomainMatches(denominator, variable, angleUnit, TrigonometricAndLatticeAnalyzer.AffineDomain(tangent, angleUnit), budget) || !DivisionDefinednessMatches(expression, numerator, denominator))
        {
            context = null!;
            return false;
        }

        ExactScalar scale = tangent.Amplitude.Reciprocal(budget);
        ExactReal punctureStep = ScaleAngle(PiAngle(angleUnit, new BigRational(1, 2)), tangent.Frequency.Reciprocal());
        var domain = new PeriodicIntervalSet(punctureStep, "m", IntegerConstraint.All("m"), [new PeriodicInterval(new RationalReal(BigRational.Zero), false, punctureStep, false)]);
        ExactReal functionPeriod = ScaleAngle(PiAngle(angleUnit, BigRational.One), tangent.Frequency.Reciprocal());
        context = new GuardedCotangentIdentityContext(expression, numerator, denominator, tangent, scale, domain, functionPeriod);
        return true;
    }

    private static bool DivisionDefinednessMatches(SemanticExpression expression, SemanticExpression numerator, SemanticExpression denominator)
    {
        var zero = new ValueTerm(-1, ValueKind.Constant, BigRational.Zero, string.Empty, [], "q:0");
        Formula expected = Formula.And(numerator.DefinedWhen, denominator.DefinedWhen, Formula.Compare(denominator.Value, Comparison.NotEqual, zero));
        return string.Equals(expression.DefinedWhen.Canonical, expected.Canonical, StringComparison.Ordinal);
    }

    private static ExactReal PiAngle(AngleUnit unit, BigRational fraction)
    {
        return unit switch
        {
            AngleUnit.Radians => new AffinePiReal(fraction, BigRational.Zero),
            AngleUnit.Degrees => new RationalReal(new BigRational(180) * fraction),
            AngleUnit.Grads => new RationalReal(new BigRational(200) * fraction),
            _ => throw new ArgumentOutOfRangeException(nameof(unit))
        };
    }

    private static ExactReal ScaleAngle(ExactReal value, BigRational scale)
    {
        return ExactRealArithmetic.Scale(value, scale);
    }
}
