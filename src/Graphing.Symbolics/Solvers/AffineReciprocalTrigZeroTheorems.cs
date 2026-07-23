namespace Graphing.Symbolics;

internal static class AffineReciprocalTrigZeroTheorems
{
    public static bool TryCompute(AffineReciprocalTrigZeroContext context, AngleUnit angleUnit, ResourceBudget budget, out RealSet zeros)
    {
        budget.Charge();
        AffineReciprocalTrigPattern pattern = context.Pattern;
        if (pattern.Function is "sec" or "csc" && context.UnitIntervalComparison > 0)
        {
            zeros = EmptySet.Instance;
            return true;
        }

        string inverse = pattern.Function switch
        {
            "sec" => "acos",
            "csc" => "asin",
            "cot" => "atan",
            _ => throw new ArgumentOutOfRangeException(nameof(context))
        };
        ExactReal principal = PrincipalInverseAngle(inverse, context.DenominatorTarget, angleUnit, budget);
        BigRational periodFraction = pattern.Function == "cot" ? BigRational.One : new BigRational(2);
        ExactReal period = ScaleAngle(Angle(angleUnit, periodFraction), pattern.Frequency.Reciprocal(), budget);
        RealSet first = new PeriodicPointSet(SolveAngle(pattern.Frequency, pattern.Phase, principal, budget), period, "m", IntegerConstraint.All("m"));
        if (pattern.Function == "cot" || context.UnitIntervalComparison == 0)
        {
            zeros = first;
            return true;
        }

        ExactReal reflected = pattern.Function == "csc" ? ExactRealArithmetic.Subtract(Angle(angleUnit, BigRational.One), principal) : ExactRealArithmetic.Negate(principal);
        RealSet second = new PeriodicPointSet(SolveAngle(pattern.Frequency, pattern.Phase, reflected, budget), period, "m", IntegerConstraint.All("m"));
        zeros = RealSets.Union(first, second);
        return true;
    }

    private static ExactReal PrincipalInverseAngle(string inverse, ExactScalar target, AngleUnit angleUnit, ResourceBudget budget)
    {
        budget.Charge();
        return ExactInverseTrigonometry.PrincipalAngle(inverse, target, angleUnit, preserveRadianZeroAsPiFraction: true);
    }

    private static ExactReal SolveAngle(BigRational frequency, BigRational phase, ExactReal angle, ResourceBudget budget)
    {
        BigRational inverseFrequency = frequency.Reciprocal();
        BigRational offset = -phase / frequency;
        budget.CheckCoefficient(inverseFrequency);
        budget.CheckCoefficient(offset);
        return ExactRealArithmetic.AddRational(ScaleAngle(angle, inverseFrequency, budget), offset);
    }

    private static ExactReal ScaleAngle(ExactReal value, BigRational scale, ResourceBudget budget)
    {
        budget.CheckCoefficient(scale);
        ExactReal result = value is FunctionReal { Function: "negate", Arguments: [var operand] } ? ExactRealArithmetic.Scale(operand, -scale) : ExactRealArithmetic.Scale(value, scale);
        if (result is AffinePiReal affine)
        {
            budget.CheckCoefficient(affine.PiCoefficient);
            budget.CheckCoefficient(affine.Constant);
        }

        return result;
    }

    private static ExactReal Angle(AngleUnit unit, BigRational piFraction)
    {
        return ExactAngleArithmetic.PiFraction(unit, piFraction);
    }
}
