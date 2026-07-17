using System.Collections.Immutable;
using System.Numerics;

namespace Graphing.Symbolics;

internal sealed record RationalReal(BigRational Value) : ExactReal;
