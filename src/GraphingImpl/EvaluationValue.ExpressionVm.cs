using System.Buffers;
using System.Collections.Immutable;
using Graphing;

namespace GraphingImpl;

internal readonly record struct EvaluationValue(double Value, EvaluationState State)
{
    public bool IsFinite => State == EvaluationState.Finite && double.IsFinite(Value);

    public static EvaluationValue Finite(double value) => double.IsFinite(value) ? new(value, EvaluationState.Finite) : new(double.NaN, double.IsInfinity(value) ? EvaluationState.Overflow : EvaluationState.Undefined);
    public static EvaluationValue Invalid(EvaluationState state) => new(double.NaN, state);
}
