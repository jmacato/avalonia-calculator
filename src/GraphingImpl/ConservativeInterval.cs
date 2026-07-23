namespace GraphingImpl;

internal readonly record struct ConservativeInterval(double Minimum, double Maximum, EvaluationState State)
{
    public bool IsFinite => State == EvaluationState.Finite && double.IsFinite(Minimum) && double.IsFinite(Maximum) && Minimum <= Maximum;

    public static ConservativeInterval Point(double value)
    {
        return double.IsFinite(value)
            ? new(value, value, EvaluationState.Finite)
            : Invalid(double.IsInfinity(value) ? EvaluationState.Overflow : EvaluationState.Undefined);
    }

    public static ConservativeInterval Invalid(EvaluationState state)
    {
        return new ConservativeInterval(double.NaN, double.NaN, state);
    }
}
