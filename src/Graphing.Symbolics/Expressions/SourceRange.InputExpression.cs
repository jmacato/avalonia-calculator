using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal readonly record struct SourceRange(int Start, int Length)
{
    public int End => checked(Start + Length);
}
