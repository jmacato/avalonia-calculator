using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record RationalPowerRangeFiberWitness(RationalRangeCellKind Kind, int Index, BigRational Sample, CellDecompositionCertificate Fiber, bool HasPreimage);
