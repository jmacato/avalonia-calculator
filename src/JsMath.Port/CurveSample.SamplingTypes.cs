using System.Collections.Immutable;
using Graphing;

namespace JsMath.Port;

public readonly record struct CurveSample(double Parameter, double X, double Y, SampleState State)
{
    public bool IsFinite => State == SampleState.Finite && double.IsFinite(X) && double.IsFinite(Y);

    public static CurveSample Undefined(double parameter, SampleState state = SampleState.Undefined) => new(parameter, double.NaN, double.NaN, state);
}
