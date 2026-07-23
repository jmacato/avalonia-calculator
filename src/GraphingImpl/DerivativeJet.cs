namespace GraphingImpl;

internal readonly record struct DerivativeJet(double Value, double First, double Second, EvaluationState State)
{
    public bool IsFinite => State == EvaluationState.Finite && double.IsFinite(Value) && double.IsFinite(First) && double.IsFinite(Second);

    public static DerivativeJet Constant(double value)
    {
        return double.IsFinite(value)
            ? new(value, 0, 0, EvaluationState.Finite)
            : Invalid(double.IsInfinity(value) ? EvaluationState.Overflow : EvaluationState.Undefined);
    }

    public static DerivativeJet Invalid(EvaluationState state)
    {
        return new DerivativeJet(double.NaN, double.NaN, double.NaN, state);
    }
}
