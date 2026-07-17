using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record SingletonReal(ExactReal Value) : RealFamily;
