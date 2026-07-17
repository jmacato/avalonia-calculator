using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record RationalRangeFiberWitness(RationalRangeCellKind Kind, int Index, BigRational Sample, CellDecompositionCertificate Fiber, bool HasPreimage)
{
    public string Canonical => $"range-fiber[{(int)Kind},{Index},{Sample},{Fiber.Formula.Canonical},{Fiber.Result.Canonical},{(HasPreimage ? 1 : 0)}]";
}
