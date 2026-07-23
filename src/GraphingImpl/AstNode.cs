using System.Collections.Immutable;

namespace GraphingImpl;

internal sealed record AstNode(AstKind Kind, SourceSpan Span, ExactRational Number, string Name, ImmutableArray<AstNode> Children)
{
    public static AstNode NumberNode(ExactRational value, SourceSpan span)
    {
        return new AstNode(AstKind.Number, span, value, string.Empty, ImmutableArray<AstNode>.Empty);
    }

    public static AstNode VariableNode(string name, SourceSpan span)
    {
        return new AstNode(AstKind.Variable, span, 0, name, ImmutableArray<AstNode>.Empty);
    }
}
