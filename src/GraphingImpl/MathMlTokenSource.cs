using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace GraphingImpl;

/// <summary>
/// Reads Presentation MathML through the BCL XML object model and exposes its
/// mathematical tokens directly to the graph syntax parser. It never creates
/// or reparses an intermediate linear equation.
/// </summary>
internal sealed class MathMlTokenSource : ITokenSource
{
    private const string FunctionApplication = "⁡";
    private readonly List<Token> _tokens = [];
    private readonly int _inputLength;
    private int _readIndex;
    private int _sourcePosition;
    private int _nodeCount;

    public MathMlTokenSource(string input, bool hasWrapper)
    {
        ArgumentNullException.ThrowIfNull(input);
        _inputLength = input.Length;
        XElement root = LoadRoot(input, hasWrapper);
        EmitChildren(root, 0);
        _tokens.Add(new Token(TokenKind.End, new SourceSpan(_sourcePosition, 0), string.Empty));
    }

    public Token Next()
    {
        return _tokens[_readIndex++];
    }

    private static XElement LoadRoot(string input, bool hasWrapper)
    {
        var settings = new XmlReaderSettings
        {
            ConformanceLevel = hasWrapper ? ConformanceLevel.Document : ConformanceLevel.Fragment,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = GraphLimits.MaximumInputLength * 4L,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true
        };

        try
        {
            using var stringReader = new StringReader(input);
            using XmlReader reader = XmlReader.Create(stringReader, settings);
            if (hasWrapper)
            {
                XDocument document = XDocument.Load(reader, LoadOptions.None);
                if (document.Root is null ||
                    !string.Equals(document.Root.Name.LocalName, "math", StringComparison.OrdinalIgnoreCase))
                {
                    throw Invalid(input.Length, "MathML input must have a math root element.");
                }

                return document.Root;
            }

            var root = new XElement("math");
            while (!reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.None)
                {
                    _ = reader.Read();
                }
                else if (reader.NodeType == XmlNodeType.Element)
                {
                    root.Add(XNode.ReadFrom(reader));
                }
                else
                {
                    _ = reader.Read();
                }
            }

            return root;
        }
        catch (GraphParseException)
        {
            throw;
        }
        catch (XmlException exception)
        {
            SyntaxErrorCode code = exception.Message.Contains("entity", StringComparison.OrdinalIgnoreCase)
                ? SyntaxErrorCode.UnknownMathMLEntity
                : SyntaxErrorCode.InvalidMathMLFormat;
            throw new GraphParseException(code, new SourceSpan(0, input.Length), exception.Message);
        }
    }

    private void EmitElement(XElement element, int depth)
    {
        CountElement(depth);
        string name = IdentifierNormalizer.ToCanonicalLowerInvariant(element.Name.LocalName);
        switch (name)
        {
            case "math":
            case "mrow":
            case "mstyle":
            case "mpadded":
            case "mphantom":
                if (!TryEmitLogFunction(element, depth))
                {
                    EmitChildren(element, depth + 1);
                }

                break;
            case "semantics":
                EmitSemantics(element, depth + 1);
                break;
            case "mn":
                EmitNumber(element.Value);
                break;
            case "mi":
                EmitIdentifier(element.Value);
                break;
            case "mo":
                EmitOperator(element.Value);
                break;
            case "mfrac":
                EmitBinaryStructure(element, TokenKind.Slash, depth + 1);
                break;
            case "msup":
                EmitBinaryStructure(element, TokenKind.Caret, depth + 1);
                break;
            case "msub":
                EmitSubscript(element);
                break;
            case "msubsup":
                EmitSubscriptSuperscript(element, depth + 1);
                break;
            case "msqrt":
                EmitFunction("sqrt", [element], depth + 1, emitChildren: true);
                break;
            case "mroot":
                EmitRoot(element, depth + 1);
                break;
            case "mfenced":
                EmitFence(element, depth + 1);
                break;
            case "none":
            case "mspace":
            case "annotation":
            case "annotation-xml":
                break;
            default:
                throw Invalid(_inputLength, $"Unsupported MathML element '{element.Name.LocalName}'.", SyntaxErrorCode.UnknownMathMLElement);
        }
    }

    private void EmitChildren(XElement element, int depth)
    {
        foreach (XElement child in element.Elements())
        {
            EmitElement(child, depth);
        }
    }

    private void EmitSemantics(XElement element, int depth)
    {
        XElement? presentation = element.Elements().FirstOrDefault(static child =>
            child.Name.LocalName is not ("annotation" or "annotation-xml"));
        if (presentation is not null)
        {
            EmitElement(presentation, depth);
        }
    }

    private void EmitNumber(string value)
    {
        string text = value.Trim().Replace('−', '-');
        try
        {
            Emit(TokenKind.Number, text, ExactRational.ParseDecimal(text));
        }
        catch (FormatException exception)
        {
            throw Invalid(_inputLength, exception.Message, SyntaxErrorCode.InvalidNumberDigit);
        }
        catch (OverflowException exception)
        {
            throw Invalid(_inputLength, exception.Message, SyntaxErrorCode.GeneralError);
        }
    }

    private void EmitIdentifier(string value)
    {
        string identifier = value.Trim().Normalize(NormalizationForm.FormC);
        Emit(TokenKind.Identifier, identifier is "π" or "Π" ? "pi" : identifier);
    }

    private void EmitOperator(string value)
    {
        string operation = value.Trim();
        TokenKind? kind = operation switch
        {
            "" or FunctionApplication => null,
            "+" or "⁤" => TokenKind.Plus,
            "-" or "−" => TokenKind.Minus,
            "*" or "×" or "⋅" or "⁢" => TokenKind.Star,
            "/" or "÷" => TokenKind.Slash,
            "^" => TokenKind.Caret,
            "(" => TokenKind.OpenParenthesis,
            ")" => TokenKind.CloseParenthesis,
            "[" => TokenKind.OpenBracket,
            "]" => TokenKind.CloseBracket,
            "{" => TokenKind.OpenBrace,
            "}" => TokenKind.CloseBrace,
            "," or "⁣" => TokenKind.Comma,
            ";" => TokenKind.Semicolon,
            "=" => TokenKind.Equal,
            "<" => TokenKind.Less,
            "<=" or "≤" => TokenKind.LessOrEqual,
            ">" => TokenKind.Greater,
            ">=" or "≥" => TokenKind.GreaterOrEqual,
            "!" => TokenKind.Bang,
            "√" => TokenKind.Radical,
            _ => throw Invalid(_inputLength, $"Unsupported MathML operator '{operation}'.", SyntaxErrorCode.InvalidToken)
        };
        if (kind is not null)
        {
            Emit(kind.Value, operation);
        }
    }

    private void EmitBinaryStructure(XElement element, TokenKind operation, int depth)
    {
        XElement[] children = RequireChildren(element, 2);
        Emit(TokenKind.OpenParenthesis, "(");
        EmitElement(children[0], depth);
        Emit(TokenKind.CloseParenthesis, ")");
        Emit(operation, operation == TokenKind.Slash ? "/" : "^");
        Emit(TokenKind.OpenParenthesis, "(");
        EmitElement(children[1], depth);
        Emit(TokenKind.CloseParenthesis, ")");
    }

    private void EmitSubscript(XElement element)
    {
        XElement[] children = RequireChildren(element, 2);
        if (!TryReadIdentifier(children[0], out string basis) ||
            !TryReadIdentifier(children[1], out string subscript))
        {
            throw Invalid(_inputLength, "A graph variable subscript must contain identifier or number tokens.");
        }

        EmitIdentifier(string.Concat(basis, "_", subscript));
    }

    private void EmitSubscriptSuperscript(XElement element, int depth)
    {
        XElement[] children = RequireChildren(element, 3);
        var subscript = new XElement("msub", new XElement(children[0]), new XElement(children[1]));
        Emit(TokenKind.OpenParenthesis, "(");
        EmitSubscript(subscript);
        Emit(TokenKind.CloseParenthesis, ")");
        Emit(TokenKind.Caret, "^");
        Emit(TokenKind.OpenParenthesis, "(");
        EmitElement(children[2], depth);
        Emit(TokenKind.CloseParenthesis, ")");
    }

    private void EmitRoot(XElement element, int depth)
    {
        XElement[] children = RequireChildren(element, 2);
        Emit(TokenKind.Identifier, "root");
        Emit(TokenKind.OpenParenthesis, "(");
        EmitElement(children[0], depth);
        Emit(TokenKind.Comma, ",");
        EmitElement(children[1], depth);
        Emit(TokenKind.CloseParenthesis, ")");
    }

    private void EmitFence(XElement element, int depth)
    {
        Emit(TokenKind.OpenParenthesis, "(");
        XElement[] children = element.Elements().ToArray();
        string separators = element.Attribute("separators")?.Value ?? ",";
        for (int index = 0; index < children.Length; index++)
        {
            if (index != 0 && separators.Length != 0)
            {
                EmitOperator(separators[Math.Min(index - 1, separators.Length - 1)].ToString());
            }

            EmitElement(children[index], depth);
        }

        Emit(TokenKind.CloseParenthesis, ")");
    }

    private bool TryEmitLogFunction(XElement element, int depth)
    {
        XElement[] children = element.Elements().ToArray();
        if (children.Length != 3 ||
            children[0].Name.LocalName != "msub" ||
            children[1].Name.LocalName != "mo" ||
            children[1].Value.Trim() != FunctionApplication ||
            children[2].Name.LocalName != "mrow")
        {
            return false;
        }

        XElement[] functionParts = children[0].Elements().ToArray();
        XElement[] argumentParts = children[2].Elements().ToArray();
        if (functionParts.Length != 2 ||
            functionParts[0].Name.LocalName != "mi" ||
            !string.Equals(functionParts[0].Value.Trim(), "log", StringComparison.OrdinalIgnoreCase) ||
            argumentParts.Length < 2 ||
            argumentParts[0].Name.LocalName != "mo" ||
            argumentParts[0].Value.Trim() != "(" ||
            argumentParts[^1].Name.LocalName != "mo" ||
            argumentParts[^1].Value.Trim() != ")")
        {
            return false;
        }

        Emit(TokenKind.Identifier, "log");
        Emit(TokenKind.OpenParenthesis, "(");
        EmitElement(functionParts[1], depth + 1);
        Emit(TokenKind.Comma, ",");
        foreach (XElement argumentPart in argumentParts[1..^1])
        {
            EmitElement(argumentPart, depth + 1);
        }

        Emit(TokenKind.CloseParenthesis, ")");
        return true;
    }

    private void EmitFunction(
        string name,
        IReadOnlyList<XElement> arguments,
        int depth,
        bool emitChildren)
    {
        Emit(TokenKind.Identifier, name);
        Emit(TokenKind.OpenParenthesis, "(");
        for (int index = 0; index < arguments.Count; index++)
        {
            if (index != 0)
            {
                Emit(TokenKind.Comma, ",");
            }

            if (emitChildren)
            {
                EmitChildren(arguments[index], depth);
            }
            else
            {
                EmitElement(arguments[index], depth);
            }
        }

        Emit(TokenKind.CloseParenthesis, ")");
    }

    private XElement[] RequireChildren(XElement element, int count)
    {
        XElement[] children = element.Elements().ToArray();
        if (children.Length != count)
        {
            throw Invalid(_inputLength, $"Element '{element.Name.LocalName}' requires {count} children.");
        }

        return children;
    }

    private static bool TryReadIdentifier(XElement element, out string value)
    {
        string name = element.Name.LocalName;
        if (name is "mi" or "mn")
        {
            value = element.Value.Trim();
            return value.Length != 0;
        }

        if (name == "mrow")
        {
            var builder = new StringBuilder();
            foreach (XElement child in element.Elements())
            {
                if (!TryReadIdentifier(child, out string childValue))
                {
                    value = string.Empty;
                    return false;
                }

                builder.Append(childValue);
            }

            value = builder.ToString();
            return value.Length != 0;
        }

        value = string.Empty;
        return false;
    }

    private void CountElement(int depth)
    {
        _nodeCount++;
        if (_nodeCount > GraphLimits.MaximumNodesPerEquation)
        {
            throw Invalid(_inputLength, "MathML contains too many elements.", SyntaxErrorCode.GeneralError);
        }

        if (depth > GraphLimits.MaximumDepth)
        {
            throw Invalid(_inputLength, "MathML is nested too deeply.", SyntaxErrorCode.GeneralError);
        }
    }

    private void Emit(TokenKind kind, string text, ExactRational number = default)
    {
        _tokens.Add(new Token(kind, new SourceSpan(_sourcePosition, 1), text, number));
        _sourcePosition++;
    }

    private static GraphParseException Invalid(
        int inputLength,
        string message,
        SyntaxErrorCode code = SyntaxErrorCode.InvalidMathMLFormat)
    {
        return new GraphParseException(code, new SourceSpan(0, inputLength), message);
    }
}
