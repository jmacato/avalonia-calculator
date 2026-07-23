using MathComposer.Core;

namespace Graphing;

public sealed record GraphMonotoneIntervalMathDocument(
    MathDocument Expression,
    int Direction);
