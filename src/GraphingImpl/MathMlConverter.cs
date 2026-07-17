using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace GraphingImpl;

internal static class MathMlConverter
{
    public static string ToLinear(string input, bool hasWrapper)
    {
        string xml = hasWrapper
            ? input
            : $"<math xmlns=\"http://www.w3.org/1998/Math/MathML\">{input}</math>";

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = GraphLimits.MaximumInputLength * 4L,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true
        };

        try
        {
            using var stringReader = new StringReader(xml);
            using XmlReader reader = XmlReader.Create(stringReader, settings);
            XDocument document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            if (document.Root is null || !string.Equals(document.Root.Name.LocalName, "math", StringComparison.OrdinalIgnoreCase))
            {
                throw new GraphParseException(
                    SyntaxErrorCode.InvalidMathMLFormat,
                    new SourceSpan(0, input.Length),
                    "MathML input must have a math root element.");
            }

            int nodeCount = 0;
            return ConvertSequence(document.Root.Nodes(), 0, ref nodeCount).Trim();
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

    private static string ConvertElement(XElement element, int depth, ref int nodeCount)
    {
        nodeCount++;
        if (nodeCount > GraphLimits.MaximumNodesPerEquation)
        {
            throw Invalid(element, SyntaxErrorCode.GeneralError, "MathML contains too many elements.");
        }

        if (depth > GraphLimits.MaximumDepth)
        {
            throw Invalid(element, SyntaxErrorCode.GeneralError, "MathML is nested too deeply.");
        }

        string name = IdentifierNormalizer.ToCanonicalLowerInvariant(element.Name.LocalName);
        return name switch
        {
            "math" or "mrow" or "mstyle" or "mpadded" or "mphantom" =>
                ConvertSequence(element.Nodes(), depth + 1, ref nodeCount),
            "semantics" => ConvertSemantics(element, depth + 1, ref nodeCount),
            "mn" => NormalizeNumber(element.Value),
            "mi" => NormalizeIdentifier(element.Value),
            "mo" => NormalizeOperator(element.Value),
            "mfrac" => ConvertFraction(element, depth + 1, ref nodeCount),
            "msup" => ConvertBinaryStructure(element, "^", depth + 1, ref nodeCount),
            "msub" => ConvertSubscript(element, depth + 1, ref nodeCount),
            "msubsup" => ConvertSubscriptSuperscript(element, depth + 1, ref nodeCount),
            "msqrt" => $"sqrt({ConvertSequence(element.Nodes(), depth + 1, ref nodeCount)})",
            "mroot" => ConvertRoot(element, depth + 1, ref nodeCount),
            "mfenced" => ConvertFence(element, depth + 1, ref nodeCount),
            "none" or "mspace" or "annotation" or "annotation-xml" => string.Empty,
            _ => throw Invalid(
                element,
                SyntaxErrorCode.UnknownMathMLElement,
                $"Unsupported MathML element '{element.Name.LocalName}'.")
        };
    }

    private static string ConvertSequence(IEnumerable<XNode> nodes, int depth, ref int nodeCount)
    {
        var builder = new StringBuilder();
        foreach (XNode node in nodes)
        {
            string value = node switch
            {
                XElement element => ConvertElement(element, depth, ref nodeCount),
                XText text when !string.IsNullOrWhiteSpace(text.Value) => text.Value.Trim(),
                _ => string.Empty
            };

            if (value.Length == 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(value);
        }

        return builder.ToString();
    }

    private static string ConvertSemantics(XElement element, int depth, ref int nodeCount)
    {
        XElement? presentation = element.Elements().FirstOrDefault(child =>
            child.Name.LocalName is not ("annotation" or "annotation-xml"));
        return presentation is null ? string.Empty : ConvertElement(presentation, depth, ref nodeCount);
    }

    private static string ConvertFraction(XElement element, int depth, ref int nodeCount)
    {
        XElement[] children = element.Elements().ToArray();
        RequireChildCount(element, children, 2);
        return $"({ConvertElement(children[0], depth, ref nodeCount)})/({ConvertElement(children[1], depth, ref nodeCount)})";
    }

    private static string ConvertBinaryStructure(
        XElement element,
        string operation,
        int depth,
        ref int nodeCount)
    {
        XElement[] children = element.Elements().ToArray();
        RequireChildCount(element, children, 2);
        return $"({ConvertElement(children[0], depth, ref nodeCount)}){operation}({ConvertElement(children[1], depth, ref nodeCount)})";
    }

    private static string ConvertSubscript(XElement element, int depth, ref int nodeCount)
    {
        XElement[] children = element.Elements().ToArray();
        RequireChildCount(element, children, 2);
        string basis = ConvertElement(children[0], depth, ref nodeCount);
        string subscript = ConvertElement(children[1], depth, ref nodeCount);
        return $"{basis}_{subscript}";
    }

    private static string ConvertSubscriptSuperscript(XElement element, int depth, ref int nodeCount)
    {
        XElement[] children = element.Elements().ToArray();
        RequireChildCount(element, children, 3);
        string basis = ConvertElement(children[0], depth, ref nodeCount);
        string subscript = ConvertElement(children[1], depth, ref nodeCount);
        string superscript = ConvertElement(children[2], depth, ref nodeCount);
        return $"({basis}_{subscript})^({superscript})";
    }

    private static string ConvertRoot(XElement element, int depth, ref int nodeCount)
    {
        XElement[] children = element.Elements().ToArray();
        RequireChildCount(element, children, 2);
        return $"root({ConvertElement(children[0], depth, ref nodeCount)},{ConvertElement(children[1], depth, ref nodeCount)})";
    }

    private static string ConvertFence(XElement element, int depth, ref int nodeCount)
    {
        string opening = element.Attribute("open")?.Value ?? "(";
        string closing = element.Attribute("close")?.Value ?? ")";
        string separators = element.Attribute("separators")?.Value ?? ",";
        string separator = separators.Length == 0 ? string.Empty : separators[0].ToString();
        var children = new List<string>();
        foreach (XElement child in element.Elements())
        {
            children.Add(ConvertElement(child, depth, ref nodeCount));
        }

        // Braces in presentation MathML are list decoration. Parentheses are
        // used in the linear form so the parser never mistakes them for input
        // document separators.
        _ = opening;
        _ = closing;
        return $"({string.Join(separator, children)})";
    }

    private static void RequireChildCount(XElement element, XElement[] children, int count)
    {
        if (children.Length != count)
        {
            throw Invalid(
                element,
                SyntaxErrorCode.InvalidMathMLFormat,
                $"Element '{element.Name.LocalName}' requires {count} children.");
        }
    }

    private static string NormalizeNumber(string value) => value.Trim().Replace('\u2212', '-');

    private static string NormalizeIdentifier(string value)
    {
        value = value.Trim();
        return value is "π" or "Π" ? "pi" : value;
    }

    private static string NormalizeOperator(string value) => value.Trim() switch
    {
        "\u2212" => "-",
        "\u00D7" or "\u22C5" or "\u2062" => "*",
        "\u00F7" => "/",
        "\u2264" => "<=",
        "\u2265" => ">=",
        "\u2061" => string.Empty,
        "\u2063" => ",",
        "\u2064" => "+",
        var operation => operation
    };

    private static GraphParseException Invalid(XElement element, SyntaxErrorCode code, string message) =>
        new(code, new SourceSpan(0, element.ToString(SaveOptions.DisableFormatting).Length), message);
}
