using System.Collections.Immutable;
using Graphing.Analyzer;
using Graphing.Symbolics;

namespace GraphingImpl;

internal enum CompatibilityValueProjectionKind
{
    SymmetricNonnegativePeriodicPoints,
    OpenFiniteIntervalEndpoints,
    EmptyZeroSetPresentation,
    ConstantPunctureHalfLines,
    HyperbolicTangentAtZero,
    DoubleFundamentalPeriod,
    ShiftedDoubleSineEndpointMinima,
    SinePhaseExtremaPresentation,
    CapturedAffineTangentPresentation
}
