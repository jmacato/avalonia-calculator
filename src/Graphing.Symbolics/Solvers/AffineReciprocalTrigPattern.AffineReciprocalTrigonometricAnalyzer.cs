using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record AffineReciprocalTrigPattern(string Function, ExactScalar Amplitude, BigRational Frequency, BigRational Phase, BigRational Shift, string RatioCanonical)
{
    public string Canonical => $"reciprocal-trig:{Function}:{Amplitude.Canonical}:{Frequency}:{Phase}:{Shift}:{RatioCanonical}";
}
