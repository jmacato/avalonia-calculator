using System.Collections.Immutable;
using Graphing;

namespace GraphingImpl;

public readonly record struct SourceSpan(int Start, int Length)
{
    public int End => Start + Length;
}

internal enum AstKind
{
    Number,
    Variable,
    Add,
    Subtract,
    Multiply,
    Divide,
    Power,
    Negate,
    Function
}

internal enum RelationKind
{
    None,
    Equal,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual
}

internal sealed record AstNode(
    AstKind Kind,
    SourceSpan Span,
    ExactRational Number,
    string Name,
    ImmutableArray<AstNode> Children)
{
    public static AstNode NumberNode(ExactRational value, SourceSpan span) =>
        new(AstKind.Number, span, value, string.Empty, ImmutableArray<AstNode>.Empty);

    public static AstNode VariableNode(string name, SourceSpan span) =>
        new(AstKind.Variable, span, 0, name, ImmutableArray<AstNode>.Empty);
}

internal sealed record EquationAst(
    AstNode Left,
    RelationKind Relation,
    AstNode? Right,
    SourceSpan Span,
    uint EquationId);

internal sealed class ManagedExpression : IExpression
{
    public ManagedExpression(
        uint expressionId,
        string source,
        FormatType sourceFormat,
        ImmutableArray<EquationAst> equations,
        ImmutableArray<string> symbols)
    {
        ExpressionId = expressionId;
        Source = source;
        SourceFormat = sourceFormat;
        Equations = equations;
        Symbols = symbols;
    }

    public uint ExpressionId { get; }

    public string Source { get; }

    public FormatType SourceFormat { get; }

    public ImmutableArray<EquationAst> Equations { get; }

    public ImmutableArray<string> Symbols { get; }

    public uint GetExpressionID() => ExpressionId;

    public bool IsEmptySet() => Equations.IsEmpty;
}

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
            throw new GraphParseException(
                SyntaxErrorCode.GeneralError,
                span,
                $"An equation may contain at most {GraphLimits.MaximumNodesPerEquation} syntax nodes.");
        }
    }
}
