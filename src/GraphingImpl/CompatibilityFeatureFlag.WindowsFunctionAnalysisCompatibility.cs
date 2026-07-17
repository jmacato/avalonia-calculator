using System.Collections.Immutable;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingImpl;

[Flags]
internal enum CompatibilityFeatureFlag
{
    None = 0,
    Domain = 1,
    Range = 2,
    Parity = 4,
    Period = 8,
    Zeros = 16,
    YIntercept = 32,
    Minima = 64,
    Maxima = 128,
    InflectionPoints = 256,
    VerticalAsymptotes = 512,
    HorizontalAsymptotes = 1024,
    ObliqueAsymptotes = 2048,
    Monotonicity = 4096
}
