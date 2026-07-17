using System.Text;

namespace GraphingImpl;

internal sealed class LatexConverter
{
    private readonly string _source;
    private int _position;

    private LatexConverter(string source)
    {
        _source = source;
    }

    public static string ToLinear(string source) => new LatexConverter(source).ConvertUntil('\0');

    private string ConvertUntil(char terminator)
    {
        var result = new StringBuilder();
        while (_position < _source.Length)
        {
            char value = _source[_position++];
            if (value == terminator)
            {
                return result.ToString();
            }

            switch (value)
            {
                case '{':
                    result.Append('(').Append(ConvertUntil('}')).Append(')');
                    break;
                case '}':
                    throw Error(SyntaxErrorCode.BracketMismatch, "A closing LaTeX brace has no matching opening brace.");
                case '\\':
                    result.Append(ConvertCommand());
                    break;
                case '~':
                    result.Append(' ');
                    break;
                case '&':
                    break;
                default:
                    result.Append(value);
                    break;
            }
        }

        if (terminator != '\0')
        {
            throw Error(SyntaxErrorCode.UnmatchedBracket, "A LaTeX group is not closed.");
        }

        return result.ToString();
    }

    private string ConvertCommand()
    {
        if (_position >= _source.Length)
        {
            throw Error(SyntaxErrorCode.UnexpectedEndOfExpression, "A LaTeX command is incomplete.");
        }

        if (!char.IsLetter(_source[_position]))
        {
            char escaped = _source[_position++];
            return escaped switch
            {
                '\\' => ";",
                '{' or '}' or '_' or '^' or '%' => escaped.ToString(),
                ',' or ';' or '!' or ' ' => " ",
                _ => throw Error(SyntaxErrorCode.InvalidToken, $"Unsupported LaTeX escape '\\{escaped}'.")
            };
        }

        int start = _position;
        while (_position < _source.Length && char.IsLetter(_source[_position]))
        {
            _position++;
        }

        string command = IdentifierNormalizer.ToCanonicalLowerInvariant(_source[start.._position]);
        return command switch
        {
            "frac" or "dfrac" or "tfrac" => ConvertFraction(),
            "sqrt" => ConvertRoot(),
            "operatorname" => ReadRequiredGroup(),
            "sin" or "cos" or "tan" or "cot" or "sec" or "csc" or
            "asin" or "acos" or "atan" or "arcsin" or "arccos" or "arctan" or
            "sinh" or "cosh" or "tanh" or
            "ln" or "log" or "exp" or "min" or "max" => command,
            "pi" => "pi",
            "infty" => "infinity",
            "cdot" or "times" => "*",
            "div" => "/",
            "pm" => "+",
            "le" or "leq" => "<=",
            "ge" or "geq" => ">=",
            "lt" => "<",
            "gt" => ">",
            "left" or "right" or "displaystyle" => string.Empty,
            _ => throw Error(SyntaxErrorCode.InvalidToken, $"Unsupported LaTeX command '\\{command}'.")
        };
    }

    private string ConvertFraction()
    {
        string numerator = ReadRequiredGroup();
        string denominator = ReadRequiredGroup();
        return $"({numerator})/({denominator})";
    }

    private string ConvertRoot()
    {
        string? index = null;
        SkipWhiteSpace();
        if (_position < _source.Length && _source[_position] == '[')
        {
            _position++;
            int start = _position;
            int close = _source.IndexOf(']', _position);
            if (close < 0)
            {
                throw Error(SyntaxErrorCode.UnmatchedBracket, "A LaTeX root index is not closed.");
            }

            index = _source[start..close];
            _position = close + 1;
        }

        string radicand = ReadRequiredGroup();
        return index is null ? $"sqrt({radicand})" : $"root({radicand},{index})";
    }

    private string ReadRequiredGroup()
    {
        SkipWhiteSpace();
        if (_position >= _source.Length || _source[_position] != '{')
        {
            throw Error(SyntaxErrorCode.UnmatchedBracket, "The LaTeX command requires a braced argument.");
        }

        _position++;
        return ConvertUntil('}');
    }

    private void SkipWhiteSpace()
    {
        while (_position < _source.Length && char.IsWhiteSpace(_source[_position]))
        {
            _position++;
        }
    }

    private GraphParseException Error(SyntaxErrorCode code, string message) =>
        new(code, new SourceSpan(Math.Max(0, _position - 1), 1), message);
}
