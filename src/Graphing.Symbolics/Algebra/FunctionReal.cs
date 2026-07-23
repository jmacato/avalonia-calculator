using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record FunctionReal(string Function, ImmutableArray<ExactReal> Arguments) : ExactReal;
