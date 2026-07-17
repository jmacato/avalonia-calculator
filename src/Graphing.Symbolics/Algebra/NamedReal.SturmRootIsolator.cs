using System.Collections.Immutable;
using System.Numerics;

namespace Graphing.Symbolics;

internal sealed record NamedReal(string Name) : ExactReal;
