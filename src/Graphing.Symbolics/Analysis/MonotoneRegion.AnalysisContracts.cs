using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record MonotoneRegion(RealSet Region, Monotonicity Direction);
