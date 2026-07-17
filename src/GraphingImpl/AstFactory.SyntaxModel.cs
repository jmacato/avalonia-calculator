using System.Collections.Immutable;
using Graphing;

namespace GraphingImpl;

internal sealed class AstFactory
{
    private int _nodeCount;
    public int NodeCount => _nodeCount;

    public AstNode Number(ExactRational value, SourceSpan span)
    {
        CountNode(span);
        return AstNode.NumberNode(value, span);
    }

    public AstNode Variable(string name, SourceSpan span)
    {
        CountNode(span);
        return AstNode.VariableNode(name, span);
    }

    public AstNode Unary(AstKind kind, AstNode child, SourceSpan span)
    {
        CountNode(span);
        return new AstNode(kind, span, 0, string.Empty, [child]);
    }

    public AstNode Binary(AstKind kind, AstNode left, AstNode right, SourceSpan span)
    {
        CountNode(span);
        return new AstNode(kind, span, 0, string.Empty, [left, right]);
    }

    public AstNode Function(string name, ImmutableArray<AstNode> arguments, SourceSpan span)
    {
        CountNode(span);
        return new AstNode(AstKind.Function, span, 0, name, arguments);
    }

    private void CountNode(SourceSpan span)
    {
        _nodeCount++;
        if (_nodeCount > GraphLimits.MaximumNodesPerEquation)
        {
            throw new GraphParseException(SyntaxErrorCode.GeneralError, span, $"An equation may contain at most {GraphLimits.MaximumNodesPerEquation} syntax nodes.");
        }
    }
}
