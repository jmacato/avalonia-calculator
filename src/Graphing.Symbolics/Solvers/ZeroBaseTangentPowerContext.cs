namespace Graphing.Symbolics;
/// <summary>
/// Exact premises for <c>0^(A*tan(a*x))</c>. A nonzero exact amplitude and a
/// nonzero rational frequency are accepted; shifted tangent positivity is left
/// to a later general sign-cell theorem rather than approximated.
/// </summary>
internal sealed record ZeroBaseTangentPowerContext(SemanticExpression Expression, SemanticExpression Exponent, AffineTrigPattern Pattern, PeriodicIntervalSet Domain, ExactReal Period)
{
    public static bool TryCreate(SemanticExpression expression, string variable, AngleUnit angleUnit, ResourceBudget budget, out ZeroBaseTangentPowerContext context)
    {
        budget.Charge();
        if (expression.Value is not { Kind: ValueKind.Power, Operands: [var basisValue, var exponentValue] } || expression.SourceOperands is not [var basis, var exponent] || basis.Value.Id != basisValue.Id || exponent.Value.Id != exponentValue.Id || !ExactScalar.TryCreate(basis.Value, budget, out ExactScalar scalarBasis) || !scalarBasis.IsZero || !TrigonometricAndLatticeAnalyzer.DomainMatches(basis, variable, angleUnit, AllRealSet.Instance, budget) || !TrigonometricAndLatticeAnalyzer.TryGetAffineTrig(exponent.Value, variable, budget, out AffineTrigPattern pattern) || pattern.Function != "tan" || pattern.Amplitude.IsZero || !pattern.Phase.IsZero || !pattern.Shift.IsZero || !TrigonometricAndLatticeAnalyzer.DomainMatches(exponent, variable, angleUnit, TrigonometricAndLatticeAnalyzer.AffineDomain(pattern, angleUnit), budget))
        {
            context = null!;
            return false;
        }

        ExactReal period = ScaleAngle(PiAngle(angleUnit, BigRational.One), pattern.Frequency.Reciprocal());
        ExactReal zero = new RationalReal(BigRational.Zero);
        ExactReal half = ScaleAngle(PiAngle(angleUnit, new BigRational(1, 2)), pattern.Frequency.Reciprocal());
        ExactReal lower = pattern.Amplitude.Sign > 0 ? zero : ExactRealArithmetic.Negate(half);
        ExactReal upper = pattern.Amplitude.Sign > 0 ? half : zero;
        var domain = new PeriodicIntervalSet(period, "m", IntegerConstraint.All("m"), [new PeriodicInterval(lower, false, upper, false)]);
        if (!PowerDefinednessMatches(expression, basis, exponent))
        {
            context = null!;
            return false;
        }

        context = new ZeroBaseTangentPowerContext(expression, exponent, pattern, domain, period);
        return true;
    }

    private static bool PowerDefinednessMatches(SemanticExpression expression, SemanticExpression basis, SemanticExpression exponent)
    {
        // The semantic builder hash-conses exact zero, so the three generic
        // variable-power branches reduce exactly to exponent > 0 here. Keep
        // the operand formulas in the reconstruction so a retained source
        // hole cannot pass merely because the value term folded to zero.
        Formula expected = Formula.And(basis.DefinedWhen, exponent.DefinedWhen, Formula.Compare(exponent.Value, Comparison.Greater, basis.Value));
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
        return value switch
        {
            RationalReal rational => new RationalReal(rational.Value * scale),
            AffinePiReal affine => new AffinePiReal(affine.PiCoefficient * scale, affine.Constant * scale),
            _ => ExactRealArithmetic.Scale(value, scale)
        };
    }
}
