using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record PresburgerJunction(bool IsConjunction, ImmutableArray<PresburgerFormula> Operands) : PresburgerFormula;
