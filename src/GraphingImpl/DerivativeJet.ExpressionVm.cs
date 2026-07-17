using System.Buffers;
using System.Collections.Immutable;
using Graphing;

namespace GraphingImpl;

internal readonly record struct DerivativeJet(double Value, double First, double Second, EvaluationState State)
{
    public bool IsFinite => State == EvaluationState.Finite && double.IsFinite(Value) && double.IsFinite(First) && double.IsFinite(Second);

    public static DerivativeJet Constant(double value) => double.IsFinite(value) ? new(value, 0, 0, EvaluationState.Finite) : Invalid(double.IsInfinity(value) ? EvaluationState.Overflow : EvaluationState.Undefined);
    public static DerivativeJet Invalid(EvaluationState state) => new(double.NaN, double.NaN, double.NaN, state);
}
