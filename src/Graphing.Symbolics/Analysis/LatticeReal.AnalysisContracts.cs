using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record LatticeReal(string Expression, ImmutableArray<string> Parameters, ImmutableArray<string> Predicates) : RealFamily;
