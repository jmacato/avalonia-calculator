using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal abstract record ExactCoefficientPattern(ExactCoefficientPatternKind Kind)
{
    public abstract string Canonical { get; }
    public abstract ImmutableArray<ExactScalar> BaseScalars { get; }
}
