using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record PresburgerBoolean(bool Value) : PresburgerFormula;
