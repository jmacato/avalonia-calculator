using System.Collections.Immutable;

namespace Graphing.Symbolics;

internal sealed record SemanticExpression(ValueTerm Value, Formula DefinedWhen, Formula ContinuousWhen, Formula DifferentiableWhen, ImmutableArray<SourceRange> Provenance, ImmutableArray<RewriteStep> RewriteHistory, ImmutableArray<SemanticExpression> SourceOperands);
