using System.Collections.Immutable;
using Graphing;
using Graphing.Analyzer;
using Graphing.Renderer;

namespace GraphingImpl;

internal sealed record CompiledGraphEquation(uint EquationId, GraphEquationKind Kind, RelationKind Relation, ExpressionProgram BoundaryProgram, ExpressionProgram? RegionProgram, AstNode BoundarySyntax, string Source, int XIndex, int YIndex, SourceSpan Span)
{
    public bool IsInequality => Relation is RelationKind.Less or RelationKind.LessOrEqual or RelationKind.Greater or RelationKind.GreaterOrEqual;

    public static CompiledGraphEquation Create(EquationAst equation, ImmutableArray<string> symbols, string source)
    {
        int xIndex = IndexOf(symbols, "x");
        int yIndex = IndexOf(symbols, "y");
        AstNode boundaryNode;
        AstNode? regionNode = null;
        GraphEquationKind kind;
        if (equation.Relation == RelationKind.None)
        {
            if (ContainsVariable(equation.Left, "y"))
            {
                throw Unsupported(equation, "An expression containing y requires an explicit relation.");
            }

            kind = GraphEquationKind.ExplicitY;
            boundaryNode = equation.Left;
        }
        else if (equation.Right is not null)
        {
            AstNode difference = Difference(equation.Left, equation.Right, equation.Span);
            if (!TryGetExplicitBoundary(equation.Left, equation.Right, out kind, out boundaryNode))
            {
                kind = GraphEquationKind.Implicit;
                boundaryNode = difference;
            }

            if (equation.Relation is RelationKind.Less or RelationKind.LessOrEqual or RelationKind.Greater or RelationKind.GreaterOrEqual)
            {
                regionNode = difference;
            }
        }
        else
        {
            throw Unsupported(equation, "The graph relation is incomplete.");
        }

        ExpressionProgram boundaryProgram = ExpressionProgram.Compile(boundaryNode, symbols);
        ExpressionProgram? regionProgram = regionNode is null ? null : ReferenceEquals(boundaryNode, regionNode) ? boundaryProgram : ExpressionProgram.Compile(regionNode, symbols);
        return new CompiledGraphEquation(equation.EquationId, kind, equation.Relation, boundaryProgram, regionProgram, boundaryNode, source, xIndex, yIndex, equation.Span);
    }

    private static bool TryGetExplicitBoundary(AstNode left, AstNode right, out GraphEquationKind kind, out AstNode boundary)
    {
        if (IsVariable(left, "y") && !ContainsVariable(right, "y"))
        {
            kind = GraphEquationKind.ExplicitY;
            boundary = right;
            return true;
        }

        if (IsVariable(right, "y") && !ContainsVariable(left, "y"))
        {
            kind = GraphEquationKind.ExplicitY;
            boundary = left;
            return true;
        }

        if (IsVariable(left, "x") && !ContainsVariable(right, "x"))
        {
            kind = GraphEquationKind.InverseX;
            boundary = right;
            return true;
        }

        if (IsVariable(right, "x") && !ContainsVariable(left, "x"))
        {
            kind = GraphEquationKind.InverseX;
            boundary = left;
            return true;
        }

        kind = default;
        boundary = null!;
        return false;
    }

    private static int IndexOf(ImmutableArray<string> symbols, string name)
    {
        for (int index = 0; index < symbols.Length; index++)
        {
            if (string.Equals(symbols[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsVariable(AstNode node, string name) => node.Kind == AstKind.Variable && string.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase);
    private static bool ContainsVariable(AstNode node, string name)
    {
        if (IsVariable(node, name))
        {
            return true;
        }

        foreach (AstNode child in node.Children)
        {
            if (ContainsVariable(child, name))
            {
                return true;
            }
        }

        return false;
    }

    private static AstNode Difference(AstNode left, AstNode right, SourceSpan span) => new(AstKind.Subtract, span, 0, string.Empty, [left, right]);
    private static GraphParseException Unsupported(EquationAst equation, string message) => new(SyntaxErrorCode.InvalidEquationSyntax, equation.Span, message);
}
