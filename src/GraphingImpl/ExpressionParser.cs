using System.Collections.Immutable;
using Graphing;

namespace GraphingImpl;

internal static class ExpressionParser
{
    public static ManagedExpression Parse(
        uint expressionId,
        uint firstEquationId,
        string input,
        FormatType format,
        LocalizationType localization)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Length > GraphLimits.MaximumInputLength)
        {
            throw new GraphParseException(
                SyntaxErrorCode.GeneralError,
                new SourceSpan(0, input.Length),
                $"Input is limited to {GraphLimits.MaximumInputLength} UTF-16 code units.");
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            return new ManagedExpression(
                expressionId,
                input,
                format,
                ImmutableArray<EquationAst>.Empty,
                ImmutableArray<string>.Empty);
        }

        LinearParser parser = format switch
        {
            FormatType.Formula or
            FormatType.InvariantFormula or
            FormatType.FormulaWithoutAggregate or
            FormatType.Linear or
            FormatType.LinearInput => new LinearParser(input, localization, firstEquationId),
            FormatType.MathML => new LinearParser(
                new MathMlTokenSource(input, hasWrapper: true),
                firstEquationId),
            FormatType.MathMLNoWrapper => new LinearParser(
                new MathMlTokenSource(input, hasWrapper: false),
                firstEquationId),
            FormatType.Latex => new LinearParser(
                LatexConverter.ToLinear(input),
                localization,
                firstEquationId),
            FormatType.MathRichEdit or
            FormatType.InlineMathRichEdit or
            FormatType.Binary or
            FormatType.InvariantBinary or
            FormatType.Base64 or
            FormatType.InvariantBase64 => throw new UnsupportedGraphFormatException(format),
            _ => throw new UnsupportedGraphFormatException(format)
        };

        (ImmutableArray<EquationAst> equations, ImmutableArray<string> symbols) = parser.Parse();
        equations = NormalizeDocument(equations, firstEquationId);
        if (equations.Length > GraphLimits.MaximumEquations)
        {
            throw new GraphParseException(
                SyntaxErrorCode.GeneralError,
                new SourceSpan(0, input.Length),
                $"At most {GraphLimits.MaximumEquations} equations are allowed.");
        }

        return new ManagedExpression(expressionId, input, format, equations, symbols);
    }

    private static ImmutableArray<EquationAst> NormalizeDocument(
        ImmutableArray<EquationAst> equations,
        uint firstEquationId)
    {
        var result = ImmutableArray.CreateBuilder<EquationAst>();
        foreach (EquationAst equation in equations)
        {
            ExpandEquation(equation, result);
        }

        for (int i = 0; i < result.Count; i++)
        {
            result[i] = result[i] with { EquationId = firstEquationId + (uint)i };
        }

        return result.ToImmutable();
    }

    private static void ExpandEquation(EquationAst equation, ImmutableArray<EquationAst>.Builder result)
    {
        if (equation.Relation == RelationKind.None && equation.Left.Kind == AstKind.Function)
        {
            string function = equation.Left.Name;
            if (function == "list")
            {
                foreach (AstNode child in equation.Left.Children)
                {
                    ExpandEquation(FromNode(child, equation), result);
                }

                return;
            }

            if (function is "plot2d" or "ploteq2d" or "plotineq2d")
            {
                foreach (AstNode child in equation.Left.Children)
                {
                    ExpandEquation(FromNode(child, equation), result);
                }

                return;
            }

            RelationKind relation = function switch
            {
                "equal" => RelationKind.Equal,
                "less" => RelationKind.Less,
                "lessorequal" => RelationKind.LessOrEqual,
                "greater" => RelationKind.Greater,
                "greaterorequal" => RelationKind.GreaterOrEqual,
                _ => RelationKind.None
            };
            if (relation != RelationKind.None)
            {
                if (equation.Left.Children.Length != 2)
                {
                    throw new GraphParseException(
                        SyntaxErrorCode.IncorrectNumParameter,
                        equation.Left.Span,
                        $"Relation '{function}' requires two arguments.");
                }

                result.Add(equation with
                {
                    Left = equation.Left.Children[0],
                    Relation = relation,
                    Right = equation.Left.Children[1]
                });
                return;
            }
        }

        result.Add(equation);
    }

    private static EquationAst FromNode(AstNode node, EquationAst source)
    {
        return new EquationAst(node, RelationKind.None, null, node.Span, source.EquationId);
    }
}
