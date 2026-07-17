using System.Collections.Immutable;
using Graphing;

namespace GraphingImpl;

internal sealed class LinearParser
{
    private readonly LinearLexer _lexer;
    private readonly AstFactory _factory = new();
    private readonly HashSet<string> _symbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly uint _firstEquationId;
    private Token _current;
    private int _depth;

    public LinearParser(string source, LocalizationType localization, uint firstEquationId)
    {
        _lexer = new LinearLexer(source, localization);
        _firstEquationId = firstEquationId;
        _current = _lexer.Next();
    }

    public (ImmutableArray<EquationAst> Equations, ImmutableArray<string> Symbols) Parse()
    {
        var equations = ImmutableArray.CreateBuilder<EquationAst>();
        while (_current.Kind != TokenKind.End)
        {
            if (equations.Count >= GraphLimits.MaximumEquations)
            {
                throw Error(SyntaxErrorCode.GeneralError, $"At most {GraphLimits.MaximumEquations} equations are allowed.");
            }

            equations.Add(ParseEquation(_firstEquationId + (uint)equations.Count));
            if (_current.Kind is TokenKind.Comma or TokenKind.Semicolon)
            {
                Advance();
                if (_current.Kind == TokenKind.End)
                {
                    throw Error(SyntaxErrorCode.UnexpectedEndOfExpression, "An equation is required after the list separator.");
                }
            }
            else if (_current.Kind != TokenKind.End)
            {
                throw Error(SyntaxErrorCode.UnexpectedToken, $"Unexpected token '{_current.Text}'.");
            }
        }

        if (_symbols.Count > GraphLimits.MaximumSymbols)
        {
            throw Error(SyntaxErrorCode.GeneralError, $"At most {GraphLimits.MaximumSymbols} symbols are allowed.");
        }

        return (
            equations.ToImmutable(),
            _symbols.Order(StringComparer.OrdinalIgnoreCase).ToImmutableArray());
    }

    private EquationAst ParseEquation(uint equationId)
    {
        int start = _current.Span.Start;
        AstNode left = ParseAdditive();
        RelationKind relation = RelationFromToken(_current.Kind);
        AstNode? right = null;
        if (relation != RelationKind.None)
        {
            Advance();
            if (_current.Kind == TokenKind.End)
            {
                throw Error(SyntaxErrorCode.UnexpectedEndOfExpression, "The equation is missing its right-hand side.");
            }

            right = ParseAdditive();
            if (RelationFromToken(_current.Kind) != RelationKind.None)
            {
                throw Error(SyntaxErrorCode.TooManyEquals, "Only one relation is allowed per equation.");
            }
        }

        int end = right?.Span.End ?? left.Span.End;
        return new EquationAst(left, relation, right, new SourceSpan(start, end - start), equationId);
    }

    private AstNode ParseAdditive()
    {
        AstNode expression = ParseMultiplicative();
        while (_current.Kind is TokenKind.Plus or TokenKind.Minus)
        {
            Token operation = _current;
            Advance();
            AstNode right = ParseMultiplicative();
            expression = _factory.Binary(
                operation.Kind == TokenKind.Plus ? AstKind.Add : AstKind.Subtract,
                expression,
                right,
                Cover(expression.Span, right.Span));
        }

        return expression;
    }

    private AstNode ParseMultiplicative()
    {
        AstNode expression = ParseUnary();
        while (true)
        {
            AstKind operation;
            if (_current.Kind is TokenKind.Star or TokenKind.Slash)
            {
                operation = _current.Kind == TokenKind.Star ? AstKind.Multiply : AstKind.Divide;
                Advance();
            }
            else if (CanStartPrimary(_current.Kind))
            {
                operation = AstKind.Multiply;
            }
            else
            {
                break;
            }

            AstNode right = ParseUnary();
            expression = _factory.Binary(operation, expression, right, Cover(expression.Span, right.Span));
        }

        return expression;
    }

    private AstNode ParseUnary()
    {
        if (_current.Kind is TokenKind.Plus or TokenKind.Minus)
        {
            Token operation = _current;
            Advance();
            AstNode operand = ParseUnary();
            return operation.Kind == TokenKind.Plus
                ? operand
                : _factory.Unary(AstKind.Negate, operand, Cover(operation.Span, operand.Span));
        }

        if (_current.Kind == TokenKind.Radical)
        {
            Token operation = _current;
            Advance();
            AstNode operand = ParseUnary();
            return _factory.Function("sqrt", [operand], Cover(operation.Span, operand.Span));
        }

        return ParsePower();
    }

    private AstNode ParsePower()
    {
        AstNode expression = ParsePostfix();
        if (_current.Kind == TokenKind.Caret)
        {
            Advance();
            AstNode exponent = ParseUnary();
            expression = _factory.Binary(AstKind.Power, expression, exponent, Cover(expression.Span, exponent.Span));
        }

        return expression;
    }

    private AstNode ParsePostfix()
    {
        AstNode expression = ParsePrimary();
        while (_current.Kind == TokenKind.Bang)
        {
            Token operation = _current;
            Advance();
            expression = _factory.Function("factorial", [expression], Cover(expression.Span, operation.Span));
        }

        return expression;
    }

    private AstNode ParsePrimary()
    {
        EnterDepth();
        try
        {
            if (_current.Kind == TokenKind.Number)
            {
                Token number = _current;
                Advance();
                return _factory.Number(number.Number, number.Span);
            }

            if (_current.Kind == TokenKind.Identifier)
            {
                Token identifier = _current;
                Advance();
                if (_current.Kind is TokenKind.OpenParenthesis or TokenKind.OpenBracket)
                {
                    return ParseFunction(identifier);
                }

                string normalizedName = IdentifierNormalizer.ToCanonicalLowerInvariant(identifier.Text);
                if (normalizedName is not ("pi" or "e" or "infinity"))
                {
                    if (normalizedName is "i")
                    {
                        throw new GraphParseException(
                            SyntaxErrorCode.CannotUseIInReal,
                            identifier.Span,
                            "The imaginary unit is not available in real graphing mode.");
                    }

                    _symbols.Add(identifier.Text);
                }

                return _factory.Variable(identifier.Text, identifier.Span);
            }

            if (_current.Kind is TokenKind.OpenParenthesis or TokenKind.OpenBrace)
            {
                Token opening = _current;
                TokenKind closing = opening.Kind == TokenKind.OpenParenthesis
                    ? TokenKind.CloseParenthesis
                    : TokenKind.CloseBrace;
                Advance();
                AstNode expression = ParseAdditive();
                if (_current.Kind != closing)
                {
                    throw Error(
                        closing == TokenKind.CloseParenthesis
                            ? SyntaxErrorCode.UnmatchedParenthesis
                            : SyntaxErrorCode.UnmatchedBracket,
                        "A grouping delimiter is not closed.");
                }

                Advance();
                return expression;
            }

            if (_current.Kind == TokenKind.End)
            {
                throw Error(SyntaxErrorCode.UnexpectedEndOfExpression, "The expression ended unexpectedly.");
            }

            if (_current.Kind is TokenKind.CloseParenthesis or TokenKind.CloseBracket or TokenKind.CloseBrace)
            {
                throw Error(SyntaxErrorCode.ParenthesisMismatch, "A closing delimiter has no matching opening delimiter.");
            }

            throw Error(SyntaxErrorCode.UnexpectedToken, $"Unexpected token '{_current.Text}'.");
        }
        finally
        {
            _depth--;
        }
    }

    private AstNode ParseFunction(Token identifier)
    {
        Token opening = _current;
        TokenKind closing = opening.Kind == TokenKind.OpenParenthesis
            ? TokenKind.CloseParenthesis
            : TokenKind.CloseBracket;
        Advance();

        var arguments = ImmutableArray.CreateBuilder<AstNode>();
        if (_current.Kind != closing)
        {
            while (true)
            {
                if (arguments.Count >= GraphLimits.MaximumArguments)
                {
                    throw Error(SyntaxErrorCode.IncorrectNumParameter, $"Functions accept at most {GraphLimits.MaximumArguments} arguments.");
                }

                arguments.Add(ParseAdditive());
                if (_current.Kind is TokenKind.Comma or TokenKind.Semicolon)
                {
                    Advance();
                    continue;
                }

                break;
            }
        }

        if (_current.Kind != closing)
        {
            throw Error(
                closing == TokenKind.CloseParenthesis
                    ? SyntaxErrorCode.UnmatchedParenthesis
                    : SyntaxErrorCode.UnmatchedBracket,
                $"Function '{identifier.Text}' has an unclosed argument list.");
        }

        Token close = _current;
        Advance();
        return _factory.Function(
            NormalizeFunctionName(identifier.Text),
            arguments.ToImmutable(),
            Cover(identifier.Span, close.Span));
    }

    private void EnterDepth()
    {
        _depth++;
        if (_depth > GraphLimits.MaximumDepth)
        {
            throw Error(SyntaxErrorCode.GeneralError, $"Expressions may be nested at most {GraphLimits.MaximumDepth} levels.");
        }
    }

    private void Advance() => _current = _lexer.Next();

    private GraphParseException Error(SyntaxErrorCode code, string message) =>
        new(code, _current.Span, message);

    private static RelationKind RelationFromToken(TokenKind kind) => kind switch
    {
        TokenKind.Equal => RelationKind.Equal,
        TokenKind.Less => RelationKind.Less,
        TokenKind.LessOrEqual => RelationKind.LessOrEqual,
        TokenKind.Greater => RelationKind.Greater,
        TokenKind.GreaterOrEqual => RelationKind.GreaterOrEqual,
        _ => RelationKind.None
    };

    private static bool CanStartPrimary(TokenKind kind) => kind is
        TokenKind.Number or
        TokenKind.Identifier or
        TokenKind.OpenParenthesis or
        TokenKind.OpenBrace or
        TokenKind.Radical;

    private static SourceSpan Cover(SourceSpan left, SourceSpan right) =>
        new(left.Start, Math.Max(left.End, right.End) - left.Start);

    private static string NormalizeFunctionName(string name) => IdentifierNormalizer.ToCanonicalLowerInvariant(name) switch
    {
        // Keep aliases out of the syntax tree so evaluation and every
        // symbolic analyzer operate on the same canonical function names.
        "arcsin" => "asin",
        "arccos" => "acos",
        "arctan" => "atan",
        "sgn" => "sign",
        "ceiling" => "ceil",
        "sum" or "plus" => "sum",
        "subtract" or "minus" => "subtract",
        "product" or "times" => "product",
        "divide" => "divide",
        "power" => "power",
        "root" => "root",
        "equal" => "equal",
        "less" => "less",
        "lessorequal" => "lessorequal",
        "greater" => "greater",
        "greaterorequal" => "greaterorequal",
        "plot2d" => "plot2d",
        "ploteq2d" => "ploteq2d",
        "plotineq2d" => "plotineq2d",
        "list" => "list",
        var normalized => normalized
    };
}
