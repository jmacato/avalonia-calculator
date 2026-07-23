namespace Graphing.Symbolics;

internal readonly record struct ZeroBaseTangentPowerCertificateReplayReplayPattern(ExactScalar Amplitude, BigRational Frequency)
{
    public string Canonical => $"tan:{Amplitude.Canonical}:{Frequency}:0:0";
}
