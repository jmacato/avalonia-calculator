using System.Collections.Immutable;
using System.Numerics;

namespace Graphing.Symbolics;

internal sealed record AlgebraicImageReal(RationalFunction Function, AlgebraicReal Argument) : ExactReal;
