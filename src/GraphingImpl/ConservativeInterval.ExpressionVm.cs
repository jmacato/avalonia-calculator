using System.Buffers;
using System.Collections.Immutable;
using Graphing;

namespace GraphingImpl;

internal readonly record struct ConservativeInterval(double Minimum, double Maximum, EvaluationState State)
{
    public bool IsFinite => State == EvaluationState.Finite && double.IsFinite(Minimum) && double.IsFinite(Maximum) && Minimum <= Maximum;

    public static ConservativeInterval Point(double value) => double.IsFinite(value) ? new(value, value, EvaluationState.Finite) : Invalid(double.IsInfinity(value) ? EvaluationState.Overflow : EvaluationState.Undefined);
    public static ConservativeInterval Invalid(EvaluationState state) => new(double.NaN, double.NaN, state);
}
