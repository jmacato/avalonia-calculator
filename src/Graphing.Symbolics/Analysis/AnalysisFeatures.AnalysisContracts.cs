using System.Collections.Immutable;

namespace Graphing.Symbolics;

[Flags]
internal enum AnalysisFeatures : uint
{
    None = 0,
    Domain = 1u << 0,
    Range = 1u << 1,
    Parity = 1u << 2,
    Zeros = 1u << 3,
    YIntercept = 1u << 4,
    Minima = 1u << 5,
    Maxima = 1u << 6,
    InflectionPoints = 1u << 7,
    VerticalAsymptotes = 1u << 8,
    HorizontalAsymptotes = 1u << 9,
    ObliqueAsymptotes = 1u << 10,
    Monotonicity = 1u << 11,
    Period = 1u << 12,
    All = (1u << 13) - 1
}
