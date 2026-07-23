using System.Text;
using Graphing;

namespace GraphingImpl;

internal sealed class LinearLexer(string source, LocalizationType localization) : ITokenSource
{
    private readonly char _decimalSeparator = localization == LocalizationType.DecimalCommaAndListSemicolon ? ',' : '.';
    private int _position;

    public Token Next()
    {
        SkipWhiteSpace();
        if (_position >= source.Length)
        {
            return new Token(TokenKind.End, new SourceSpan(_position, 0), string.Empty);
        }

        int start = _position;
        char current = source[_position++];
        return current switch
        {
            '+' => Single(TokenKind.Plus, start),
            '-' or '\u2212' => Single(TokenKind.Minus, start),
            '*' or '\u00D7' or '\u22C5' or '\u2062' => Single(TokenKind.Star, start),
            '/' or '\u00F7' => Single(TokenKind.Slash, start),
            '^' => Single(TokenKind.Caret, start),
            '(' => Single(TokenKind.OpenParenthesis, start),
            ')' => Single(TokenKind.CloseParenthesis, start),
            '[' => Single(TokenKind.OpenBracket, start),
            ']' => Single(TokenKind.CloseBracket, start),
            '{' => Single(TokenKind.OpenBrace, start),
            '}' => Single(TokenKind.CloseBrace, start),
            ',' => Single(TokenKind.Comma, start),
            ';' => Single(TokenKind.Semicolon, start),
            '=' => Single(TokenKind.Equal, start),
            '<' => Match('=') ? TokenFrom(TokenKind.LessOrEqual, start) : Single(TokenKind.Less, start),
            '>' => Match('=') ? TokenFrom(TokenKind.GreaterOrEqual, start) : Single(TokenKind.Greater, start),
            '\u2264' => Single(TokenKind.LessOrEqual, start),
            '\u2265' => Single(TokenKind.GreaterOrEqual, start),
            '!' => Single(TokenKind.Bang, start),
            '\u221A' => Single(TokenKind.Radical, start),
            '\u2061' => Next(),
            _ when char.IsDigit(current) || current == _decimalSeparator => ReadNumber(start),
            _ when IsIdentifierStart(current) => ReadIdentifier(start),
            _ => throw new GraphParseException(SyntaxErrorCode.InvalidToken, new SourceSpan(start, 1), $"Invalid character U+{(int)current:X4}.")
        };
    }

    private Token ReadNumber(int start)
    {
        bool sawDecimal = source[start] == _decimalSeparator;
        bool sawDigit = char.IsDigit(source[start]);
        int digitCount = sawDigit ? 1 : 0;
        while (_position < source.Length)
        {
            char value = source[_position];
            if (char.IsDigit(value))
            {
                sawDigit = true;
                digitCount++;
                _position++;
                continue;
            }

            if (value == _decimalSeparator)
            {
                if (sawDecimal)
                {
                    throw new GraphParseException(SyntaxErrorCode.TooManyDecimalPoints, new SourceSpan(start, _position - start + 1), "A number contains more than one decimal separator.");
                }

                sawDecimal = true;
                _position++;
                continue;
            }

            break;
        }

        if (!sawDigit)
        {
            throw new GraphParseException(SyntaxErrorCode.DecimalPointWithoutDigits, new SourceSpan(start, _position - start), "A decimal separator must have an adjacent digit.");
        }

        // 4096 binary digits is approximately 1234 decimal digits. Rejecting
        // before conversion also avoids pathological parser work.
        if (digitCount > 1_234)
        {
            throw new GraphParseException(SyntaxErrorCode.GeneralError, new SourceSpan(start, _position - start), $"Exact values are limited to {GraphLimits.MaximumExactValueBits} bits.");
        }

        if (_position < source.Length && source[_position] is 'e' or 'E')
        {
            int exponentStart = _position++;
            if (_position < source.Length && source[_position] is '+' or '-')
            {
                _position++;
            }

            int exponentDigits = _position;
            while (_position < source.Length && char.IsDigit(source[_position]))
            {
                _position++;
            }

            if (_position == exponentDigits)
            {
                _position = exponentStart;
            }
        }

        string text = source[start.._position];
        string invariant = _decimalSeparator == '.' ? text : text.Replace(',', '.');
        ExactRational number;
        try
        {
            number = ExactRational.ParseDecimal(invariant);
        }
        catch (OverflowException)
        {
            throw new GraphParseException(SyntaxErrorCode.GeneralError, new SourceSpan(start, _position - start), $"Exact values are limited to {GraphLimits.MaximumExactValueBits} bits.");
        }
        catch (FormatException)
        {
            throw new GraphParseException(SyntaxErrorCode.InvalidNumberDigit, new SourceSpan(start, _position - start), "The numeric literal is invalid or outside the supported range.");
        }

        return new Token(TokenKind.Number, new SourceSpan(start, _position - start), text, number);
    }

    private Token ReadIdentifier(int start)
    {
        while (_position < source.Length && IsIdentifierPart(source[_position]))
        {
            _position++;
        }

        string value = source[start.._position].Normalize(NormalizationForm.FormC);
        if (string.Equals(value, "π", StringComparison.Ordinal))
        {
            value = "pi";
        }

        return new Token(TokenKind.Identifier, new SourceSpan(start, _position - start), value);
    }

    private Token Single(TokenKind kind, int start)
    {
        return new Token(kind, new SourceSpan(start, 1), source.Substring(start, 1));
    }

    private Token TokenFrom(TokenKind kind, int start)
    {
        return new Token(kind, new SourceSpan(start, _position - start), source[start.._position]);
    }

    private bool Match(char expected)
    {
        if (_position >= source.Length || source[_position] != expected)
        {
            return false;
        }

        _position++;
        return true;
    }

    private void SkipWhiteSpace()
    {
        while (_position < source.Length && char.IsWhiteSpace(source[_position]))
        {
            _position++;
        }
    }

    private static bool IsIdentifierStart(char value)
    {
        return char.IsLetter(value) || value is '_' or '\u03C0' or '\u03A0';
    }

    private static bool IsIdentifierPart(char value)
    {
        return char.IsLetterOrDigit(value) || value is '_' or '\u2032' or '\u2033';
    }
}
