using System.Collections.Immutable;
using Graphing;

namespace GraphingImpl;

internal sealed record GraphSnapshot(ImmutableArray<CompiledGraphEquation> Definitions, ImmutableArray<ManagedEquation> Equations, ImmutableArray<IVariable> Variables, ImmutableArray<string> Symbols, ImmutableArray<double> Values, long Revision)
{
    public static GraphSnapshot Empty { get; } = new(ImmutableArray<CompiledGraphEquation>.Empty, ImmutableArray<ManagedEquation>.Empty, ImmutableArray<IVariable>.Empty, ImmutableArray<string>.Empty, ImmutableArray<double>.Empty, 0);
}
