namespace GraphingImpl;

internal readonly record struct EvaluationValue(double Value, EvaluationState State)
{
    public bool IsFinite => State == EvaluationState.Finite && double.IsFinite(Value);

    public static EvaluationValue Finite(double value)
    {
        return double.IsFinite(value)
            ? new(value, EvaluationState.Finite)
            : new(double.NaN, double.IsInfinity(value) ? EvaluationState.Overflow : EvaluationState.Undefined);
    }

    public static EvaluationValue Invalid(EvaluationState state)
    {
        return new EvaluationValue(double.NaN, state);
    }
}
