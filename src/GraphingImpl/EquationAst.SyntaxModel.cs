using System.Collections.Immutable;
using Graphing;

namespace GraphingImpl;

internal sealed record EquationAst(AstNode Left, RelationKind Relation, AstNode? Right, SourceSpan Span, uint EquationId);
