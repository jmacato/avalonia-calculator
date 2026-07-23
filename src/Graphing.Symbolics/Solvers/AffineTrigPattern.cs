namespace Graphing.Symbolics;

internal sealed record AffineTrigPattern(string Function, ExactScalar Amplitude, BigRational Frequency, BigRational Phase, BigRational Shift)
{
    public string Canonical => $"{Function}:{Amplitude.Canonical}:{Frequency}:{Phase}:{Shift}";
}
