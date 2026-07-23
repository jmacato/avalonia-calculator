namespace Graphing.Symbolics;

internal readonly record struct SingleHoleSineCertificateReplayReplayPattern(ExactScalar Amplitude, BigRational Frequency)
{
    public string Canonical => $"sin:{Amplitude.Canonical}:{Frequency}:0:0";
}
