using System.Collections.Immutable;
using Graphing;

namespace GraphingImpl;

public readonly record struct SourceSpan(int Start, int Length)
{
    public int End => Start + Length;
}
