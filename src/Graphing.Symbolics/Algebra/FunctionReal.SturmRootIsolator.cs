using System.Collections.Immutable;
using System.Numerics;

namespace Graphing.Symbolics;

internal sealed record FunctionReal(string Function, ImmutableArray<ExactReal> Arguments) : ExactReal;
