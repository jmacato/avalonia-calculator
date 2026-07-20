using System.Collections.Immutable;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace MathComposer.Core;

internal sealed class MathMlImporter
{
    private const string MathMlNamespace = MathMlSerializer.NamespaceUri;

    private static readonly HashSet<string> PresentationElements = new(StringComparer.Ordinal)
        {
            "math", "mrow", "mi", "mn", "mo", "mtext", "mspace", "mfrac", "msqrt",
            "mroot", "msub", "msup", "msubsup", "munder", "mover", "munderover",
            "mtable", "mtr", "mtd", "menclose", "merror", "semantics", "mfenced"
        };

    private static readonly HashSet<string> FunctionNames = new(StringComparer.Ordinal)
        {
            "ln", "log", "exp",
            "sin", "cos", "tan", "sec", "csc", "cot",
            "asin", "acos", "atan", "asec", "acsc", "acot",
            "sinh", "cosh", "tanh", "sech", "csch", "coth",
            "asinh", "acosh", "atanh", "asech", "acsch", "acoth"
        };

    private static readonly HashSet<string> RelationOperators = new(StringComparer.Ordinal)
        {
            "=", "≠", "<", ">", "≤", "≥", "≈", "≡", "∈", "∉", "⊂", "⊃"
        };

    private static readonly HashSet<string> PunctuationOperators = new(StringComparer.Ordinal)
        {
            ",", ";", ":"
        };

    private static readonly Dictionary<string, HashSet<string>> AllowedAttributes =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["math"] = Set("display"),
            ["mi"] = Set("mathvariant"),
            ["mn"] = Set(),
            ["mo"] = Set("stretchy", "fence", "separator", "largeop"),
            ["mtext"] = Set(),
            ["mspace"] = Set("width"),
            ["mfrac"] = Set("linethickness"),
            ["mover"] = Set("accent"),
            ["munder"] = Set("accentunder"),
            ["munderover"] = Set("accent", "accentunder"),
            ["mtable"] = Set("data-math-composer-layout"),
            ["mfenced"] = Set("open", "close", "separators"),
            ["menclose"] = Set("notation"),
            ["annotation"] = Set("encoding")
        };

    private readonly string _source;
    private readonly List<MathDiagnostic> _diagnostics = [];
    private readonly int[] _lineStarts;

    public MathMlImporter(string source)
    {
        _source = source;
        _lineStarts = FindLineStarts(source);
    }

    public MathParseResult Parse()
    {
        var settings = new XmlReaderSettings
        {
            CheckCharacters = true,
            ConformanceLevel = ConformanceLevel.Document,
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false,
            IgnoreWhitespace = false,
            MaxCharactersFromEntities = MathImportLimits.MaximumInputUtf8Bytes,
            MaxCharactersInDocument = MathImportLimits.MaximumInputUtf8Bytes,
            XmlResolver = null
        };

        XDocument xmlDocument;
        using (var stringReader = new StringReader(_source))
        using (XmlReader reader = XmlReader.Create(stringReader, settings))
        {
            xmlDocument = XDocument.Load(
                reader,
                LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        }

        foreach (XProcessingInstruction instruction in
                 xmlDocument.DescendantNodes().OfType<XProcessingInstruction>())
        {
            AddDiagnostic(
                "MC3104",
                MathDiagnosticSeverity.Error,
                "Processing instructions are ignored.",
                instruction);
        }

        XElement? root = xmlDocument.Root;
        if (root is null)
        {
            return new MathParseResult(
                new MathDocument(new MathRow([
                    CreateError(_source, "MC3101", "The XML document has no root element.", null)
                ])),
                _diagnostics.ToImmutableArray());
        }

        ValidateXmlTree(root);
        MathRow documentRoot;
        if (!IsMathMlElement(root, "math"))
        {
            documentRoot = new MathRow([
                CreateElementError(root, "MC3102", "The XML root must be a MathML math element.")
            ]);
        }
        else if (HasSecurityAttribute(root))
        {
            documentRoot = new MathRow([
                CreateElementError(root, "MC3103", "A security-sensitive attribute was rejected.")
            ]);
        }
        else
        {
            ValidateUnknownAttributes(root);
            documentRoot = ConvertContentRow(root);
        }

        ValidateProducedDocument(documentRoot);
        return new MathParseResult(
            new MathDocument(documentRoot),
            _diagnostics.ToImmutableArray());
    }

    private MathNode ConvertElement(XElement element)
    {
        if (!IsSupportedNamespace(element))
        {
            return CreateElementError(
                element,
                "MC3102",
                "Elements outside the Presentation MathML namespace are unsupported.");
        }

        if (HasSecurityAttribute(element))
        {
            return CreateElementError(
                element,
                "MC3103",
                "A security-sensitive attribute was rejected.");
        }

        ValidateUnknownAttributes(element);
        return element.Name.LocalName switch
        {
            "math" or "mrow" => ConvertRow(element),
            "mi" => ConvertToken(element, MathAtomClass.Identifier),
            "mn" => ConvertToken(element, MathAtomClass.Number),
            "mo" => ConvertOperator(element),
            "mtext" => ConvertToken(element, MathAtomClass.OrdinaryText),
            "mspace" => ConvertSpace(element),
            "mfrac" => ConvertFraction(element),
            "msqrt" => new MathRadical(ConvertContentRow(element)),
            "mroot" => ConvertRoot(element),
            "msub" => ConvertScript(element, hasSubscript: true, hasSuperscript: false),
            "msup" => ConvertScript(element, hasSubscript: false, hasSuperscript: true),
            "msubsup" => ConvertScript(element, hasSubscript: true, hasSuperscript: true),
            "munder" or "mover" or "munderover" => ConvertUnderOver(element),
            "mtable" => ConvertTable(element, inferredKind: null),
            "mfenced" => ConvertFenced(element),
            "menclose" => ConvertEnclose(element),
            "merror" => ConvertMerror(element),
            "semantics" => ConvertSemantics(element),
            "annotation" => CreateElementError(
                element,
                "MC3110",
                "An annotation is only accepted as a constrained semantics fallback."),
            "annotation-xml" => CreateElementError(
                element,
                "MC3110",
                "XML annotations are unsupported."),
            "mtr" or "mtd" => CreateElementError(
                element,
                "MC3102",
                "Table row and cell elements are only valid inside a table."),
            _ => CreateElementError(
                element,
                "MC3102",
                "The Presentation MathML element is unsupported.")
        };
    }

    private MathNode ConvertRow(XElement element)
    {
        if (TryConvertFunction(element, out MathNode? function))
        {
            return function!;
        }

        if (TryConvertDelimiter(element, out MathNode? delimiter))
        {
            return delimiter!;
        }

        return ConvertContentRow(element);
    }

    private MathNode ConvertToken(XElement element, MathAtomClass atomClass)
    {
        if (element.HasElements)
        {
            return CreateElementError(
                element,
                "MC3105",
                "MathML token elements cannot contain child elements.");
        }

        string value = element.Value;
        if (value.Length == 0 || !UnicodeScalarText.IsWellFormed(value))
        {
            return CreateElementError(
                element,
                "MC3105",
                "The MathML token is empty or contains invalid Unicode.");
        }

        EnsureTokenLimit(value, element);
        return new MathText(value, atomClass);
    }

    private MathNode ConvertOperator(XElement element)
    {
        if (element.HasElements)
        {
            return CreateElementError(
                element,
                "MC3105",
                "MathML operator elements cannot contain child elements.");
        }

        string value = element.Value;
        if (value.Length == 0 || !UnicodeScalarText.IsWellFormed(value))
        {
            return CreateElementError(
                element,
                "MC3105",
                "The MathML operator is empty or contains invalid Unicode.");
        }

        EnsureTokenLimit(value, element);
        MathAtomClass atomClass = RelationOperators.Contains(value)
            ? MathAtomClass.Relation
            : PunctuationOperators.Contains(value)
                ? MathAtomClass.Punctuation
                : MathAtomClass.Operator;
        return new MathText(value, atomClass);
    }

    private MathNode ConvertSpace(XElement element)
    {
        string? width = ((string?)element.Attribute("width"))?.Trim();
        MathSpacingWidth? spacing =
            string.Equals(width, "thinmathspace", StringComparison.OrdinalIgnoreCase) ||
            width is "0.166667em" or "0.166666em" or "3/18em"
                ? MathSpacingWidth.Thin
                : string.Equals(width, "mediummathspace", StringComparison.OrdinalIgnoreCase) ||
                  width is "0.222222em" or "4/18em"
                    ? MathSpacingWidth.Medium
                    : width is "1em" or "1.0em"
                        ? MathSpacingWidth.Em
                        : null;
        return spacing is not null
            ? new MathSpacing(spacing.Value)
            : CreateElementError(
                element,
                "MC3105",
                "The mspace width is outside the supported canonical widths.");
    }

    private MathNode ConvertFraction(XElement element)
    {
        XElement[] children = element.Elements().ToArray();
        if (children.Length != 2)
        {
            return CreateArityError(element, "mfrac requires exactly two child elements.");
        }

        return new MathFraction(ToSlotRow(children[0]), ToSlotRow(children[1]));
    }

    private MathNode ConvertRoot(XElement element)
    {
        XElement[] children = element.Elements().ToArray();
        if (children.Length != 2)
        {
            return CreateArityError(element, "mroot requires a radicand and a degree.");
        }

        return new MathRadical(ToSlotRow(children[0]), ToSlotRow(children[1]));
    }

    private MathNode ConvertScript(
        XElement element,
        bool hasSubscript,
        bool hasSuperscript)
    {
        XElement[] children = element.Elements().ToArray();
        int required = 1 + (hasSubscript ? 1 : 0) + (hasSuperscript ? 1 : 0);
        if (children.Length != required)
        {
            return CreateArityError(element, $"{element.Name.LocalName} has invalid arity.");
        }

        MathNode @base = ConvertElement(children[0]);
        int index = 1;
        MathRow? subscript = hasSubscript ? ToSlotRow(children[index++]) : null;
        MathRow? superscript = hasSuperscript ? ToSlotRow(children[index]) : null;
        return new MathScript(@base, subscript, superscript);
    }

    private MathNode ConvertUnderOver(XElement element)
    {
        XElement[] children = element.Elements().ToArray();
        bool hasBelow = element.Name.LocalName is "munder" or "munderover";
        bool hasAbove = element.Name.LocalName is "mover" or "munderover";
        int required = 1 + (hasBelow ? 1 : 0) + (hasAbove ? 1 : 0);
        if (children.Length != required)
        {
            return CreateArityError(element, $"{element.Name.LocalName} has invalid arity.");
        }

        MathNode @base = ConvertElement(children[0]);
        int index = 1;
        XElement? belowElement = hasBelow ? children[index++] : null;
        XElement? aboveElement = hasAbove ? children[index] : null;

        if (IsNaryBase(@base))
        {
            return new MathUnderOver(
                AsRow(@base),
                belowElement is null ? null : ToSlotRow(belowElement),
                aboveElement is null ? null : ToSlotRow(aboveElement),
                MathUnderOverKind.NaryLimits);
        }

        if (@base is MathUnderOver construction)
        {
            return new MathUnderOver(
                construction.Base,
                belowElement is null ? construction.Below : ToSlotRow(belowElement),
                aboveElement is null ? construction.Above : ToSlotRow(aboveElement),
                construction.Kind);
        }

        XElement? markerElement = belowElement ?? aboveElement;
        string marker = markerElement?.Value ?? string.Empty;
        bool markerBelow = belowElement is not null;
        if (TryGetConstruction(marker, markerBelow, out MathUnderOverKind constructionKind))
        {
            return new MathUnderOver(AsRow(@base), null, null, constructionKind);
        }

        if (TryGetAccent(marker, out MathAccentKind accentKind))
        {
            return new MathAccent(
                AsRow(@base),
                accentKind,
                markerBelow ? MathAccentPlacement.Under : MathAccentPlacement.Over);
        }

        return CreateElementError(
            element,
            "MC3105",
            "Only supported accents, bars, braces, and n-ary limits may use under/over elements.");
    }

    private MathNode ConvertTable(XElement element, MathTableKind? inferredKind)
    {
        var rows = new List<List<MathRow>>();
        foreach (XElement rowElement in element.Elements())
        {
            if (!IsMathMlElement(rowElement, "mtr"))
            {
                rows.Add([AsRow(CreateElementError(
                        rowElement,
                        "MC3102",
                        "Only mtr elements may be direct children of mtable."))]);
                continue;
            }

            if (HasSecurityAttribute(rowElement))
            {
                rows.Add([AsRow(CreateElementError(
                        rowElement,
                        "MC3103",
                        "A security-sensitive attribute was rejected."))]);
                continue;
            }

            ValidateUnknownAttributes(rowElement);
            var cells = new List<MathRow>();
            foreach (XElement cellElement in rowElement.Elements())
            {
                if (IsMathMlElement(cellElement, "mtd"))
                {
                    if (HasSecurityAttribute(cellElement))
                    {
                        cells.Add(AsRow(CreateElementError(
                            cellElement,
                            "MC3103",
                            "A security-sensitive attribute was rejected.")));
                    }
                    else
                    {
                        ValidateUnknownAttributes(cellElement);
                        cells.Add(ConvertCellRow(cellElement));
                    }
                }
                else
                {
                    cells.Add(AsRow(CreateElementError(
                        cellElement,
                        "MC3102",
                        "Only mtd elements may be direct children of mtr.")));
                }
            }

            if (cells.Count == 0)
            {
                cells.Add(MathRow.Empty);
            }

            rows.Add(cells);
        }

        if (rows.Count == 0)
        {
            return CreateElementError(element, "MC3105", "An mtable requires at least one row.");
        }

        int columnCount = rows.Max(static row => row.Count);
        bool padded = false;
        foreach (List<MathRow> row in rows)
        {
            while (row.Count < columnCount)
            {
                row.Add(MathRow.Empty);
                padded = true;
            }
        }

        if (padded)
        {
            AddDiagnostic(
                "MC3107",
                MathDiagnosticSeverity.Warning,
                "Short MathML table rows were padded with empty cells.",
                element);
        }

        MathTableKind kind = ReadTableKind(element, inferredKind);
        if (kind == MathTableKind.Gathered && columnCount != 1)
        {
            return CreateElementError(
                element,
                "MC3108",
                "A gathered MathML table must have exactly one column.");
        }

        ImmutableArray<ImmutableArray<MathRow>> immutableRows = rows
            .Select(static row => row.ToImmutableArray())
            .ToImmutableArray();
        return new MathTable(immutableRows, kind);
    }

    private MathTableKind ReadTableKind(XElement element, MathTableKind? inferredKind)
    {
        string? value = (string?)element.Attribute("data-math-composer-layout");
        if (value is not null)
        {
            string normalized = value.Trim();
            if (normalized.Equals("matrix", StringComparison.OrdinalIgnoreCase))
            {
                return MathTableKind.Matrix;
            }

            if (normalized.Equals("cases", StringComparison.OrdinalIgnoreCase))
            {
                return MathTableKind.Cases;
            }

            if (normalized.Equals("aligned", StringComparison.OrdinalIgnoreCase))
            {
                return MathTableKind.Aligned;
            }

            return normalized.Equals("gathered", StringComparison.OrdinalIgnoreCase)
                ? MathTableKind.Gathered
                : ReportUnknownTableLayout(element);
        }

        if (inferredKind is not null)
        {
            return inferredKind.Value;
        }

        AddDiagnostic(
            "MC3109",
            MathDiagnosticSeverity.Warning,
            "The table layout was not declared; matrix layout was assumed.",
            element);
        return MathTableKind.Matrix;
    }

    private MathTableKind ReportUnknownTableLayout(XElement element)
    {
        AddDiagnostic(
            "MC3109",
            MathDiagnosticSeverity.Warning,
            "The table layout value is unknown; matrix layout was assumed.",
            element);
        return MathTableKind.Matrix;
    }

    private MathNode ConvertFenced(XElement element)
    {
        string openingText = (string?)element.Attribute("open") ?? "(";
        string closingText = (string?)element.Attribute("close") ?? ")";
        string? opening = ParseDelimiterValue(openingText, element);
        string? closing = ParseDelimiterValue(closingText, element);
        if ((openingText.Length > 0 && opening is null) ||
            (closingText.Length > 0 && closing is null) ||
            (opening is null && closing is null))
        {
            return CreateElementError(
                element,
                "MC3105",
                "mfenced delimiters must each contain zero or one Unicode scalar.");
        }

        XElement[] children = element.Elements().ToArray();
        string separators = (string?)element.Attribute("separators") ?? ",";
        string[] separatorScalars = separators
            .EnumerateRunes()
            .Where(static rune => !Rune.IsWhiteSpace(rune))
            .Select(static rune => rune.ToString())
            .ToArray();
        var body = new List<MathNode>();
        for (int index = 0; index < children.Length; index++)
        {
            if (index > 0 && separatorScalars.Length > 0)
            {
                string separator = separatorScalars[Math.Min(index - 1, separatorScalars.Length - 1)];
                body.Add(new MathText(
                    separator,
                    PunctuationOperators.Contains(separator)
                        ? MathAtomClass.Punctuation
                        : MathAtomClass.Operator));
            }

            body.Add(ConvertElement(children[index]));
        }

        return new MathDelimiter(new MathRow(body), opening, closing, scalable: true);
    }

    private MathNode ConvertEnclose(XElement element)
    {
        string notation = ((string?)element.Attribute("notation") ?? string.Empty).Trim();
        MathUnderOverKind? kind = notation switch
        {
            "top" => MathUnderOverKind.Overbar,
            "bottom" => MathUnderOverKind.Underbar,
            _ => null
        };
        return kind is not null
            ? new MathUnderOver(ConvertContentRow(element), null, null, kind.Value)
            : CreateElementError(
                element,
                "MC3105",
                "Only top and bottom menclose notation is supported.");
    }

    private MathError ConvertMerror(XElement element)
    {
        XElement? textElement = element.Elements().FirstOrDefault(
            child => IsMathMlElement(child, "mtext"));
        string raw = textElement?.Value ?? element.Value;
        const string code = "MC3190";
        const string message = "The MathML source contains an explicit error element.";
        AddDiagnostic(code, MathDiagnosticSeverity.Error, message, element);
        return new MathError(MathTextFormat.MathMl, raw, code, message);
    }

    private MathNode ConvertSemantics(XElement element)
    {
        XElement[] children = element.Elements().ToArray();
        XElement? presentation = children.FirstOrDefault(child =>
            IsSupportedNamespace(child) && PresentationElements.Contains(child.Name.LocalName));
        if (presentation is not null)
        {
            foreach (XElement annotation in children.Where(static child =>
                         child.Name.LocalName is "annotation" or "annotation-xml"))
            {
                AddDiagnostic(
                    "MC3110",
                    MathDiagnosticSeverity.Info,
                    "A semantics annotation was ignored in favor of presentation markup.",
                    annotation);
            }

            return ConvertElement(presentation);
        }

        foreach (XElement annotation in children.Where(child =>
                     IsMathMlElement(child, "annotation")))
        {
            string? encoding = (string?)annotation.Attribute("encoding");
            MathParseResult? fallback = ParseAnnotation(annotation.Value, encoding);
            if (fallback is null)
            {
                AddDiagnostic(
                    "MC3110",
                    MathDiagnosticSeverity.Warning,
                    "An annotation with an unsupported encoding was ignored.",
                    annotation);
                continue;
            }

            AddDiagnostic(
                "MC3111",
                MathDiagnosticSeverity.Info,
                "A plain-text semantics annotation was used as a fallback.",
                annotation);
            _diagnostics.AddRange(fallback.Diagnostics);
            return fallback.Document.Root;
        }

        return CreateElementError(
            element,
            "MC3110",
            "The semantics element has no supported presentation child or text fallback.");
    }

    private static MathParseResult? ParseAnnotation(string text, string? encoding)
    {
        string normalized = encoding?.Trim() ?? string.Empty;
        if (normalized.Equals("UnicodeMath", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("application/x-unicodemath", StringComparison.OrdinalIgnoreCase))
        {
            return UnicodeMathParser.Parse(text);
        }

        if (normalized.Equals("LaTeX", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("TeX", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("application/x-tex", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("application/x-latex", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("application/latex", StringComparison.OrdinalIgnoreCase))
        {
            return LatexParser.Parse(text);
        }

        return null;
    }

    private bool TryConvertFunction(XElement element, out MathNode? node)
    {
        node = null;
        if (element.Name.LocalName != "mrow")
        {
            return false;
        }

        XElement[] children = element.Elements().ToArray();
        if (children.Length != 3 ||
            !IsMathMlElement(children[1], "mo") || children[1].Value != "⁡" ||
            !TryGetParenthesizedArgument(children[2], out XElement? argumentElement))
        {
            return false;
        }

        string? name = null;
        MathRow? logarithmBase = null;
        if (IsMathMlElement(children[0], "mi"))
        {
            name = children[0].Value;
        }
        else if (IsMathMlElement(children[0], "msub"))
        {
            XElement[] baseChildren = children[0].Elements().ToArray();
            if (baseChildren.Length == 2 &&
                IsMathMlElement(baseChildren[0], "mi") &&
                baseChildren[0].Value == "log")
            {
                name = "log";
                logarithmBase = ToSlotRow(baseChildren[1]);
            }
        }

        if (name is null || !FunctionNames.Contains(name))
        {
            return false;
        }

        MathRow argument = ToSlotRow(argumentElement!);
        node = logarithmBase is null
            ? new MathFunction(name, [argument])
            : new MathFunction(name, [logarithmBase, argument]);
        return true;
    }

    private static bool TryGetParenthesizedArgument(
        XElement element,
        out XElement? argumentElement)
    {
        argumentElement = null;
        if (!IsMathMlElement(element, "mrow"))
        {
            return false;
        }

        XElement[] children = element.Elements().ToArray();
        if (children.Length != 3 ||
            !IsMathMlElement(children[0], "mo") || children[0].Value != "(" ||
            !IsMathMlElement(children[2], "mo") || children[2].Value != ")")
        {
            return false;
        }

        argumentElement = children[1];
        return true;
    }

    private bool TryConvertDelimiter(XElement element, out MathNode? node)
    {
        node = null;
        if (element.Name.LocalName != "mrow")
        {
            return false;
        }

        XElement[] children = element.Elements().ToArray();
        if (children.Length != 3 ||
            !IsMathMlElement(children[0], "mo") ||
            !IsMathMlElement(children[2], "mo"))
        {
            return false;
        }

        string openingText = children[0].Value;
        string closingText = children[2].Value;
        string? opening = ParseDelimiterValue(openingText, children[0]);
        string? closing = ParseDelimiterValue(closingText, children[2]);
        if ((openingText.Length > 0 && opening is null) ||
            (closingText.Length > 0 && closing is null) ||
            (opening is null && closing is null))
        {
            return false;
        }

        bool recognizedPair = IsDelimiterSymbol(opening) && IsDelimiterSymbol(closing);
        if (!recognizedPair)
        {
            return false;
        }

        MathRow body;
        if (IsMathMlElement(children[1], "mtable"))
        {
            MathTableKind? inferred = opening == "{" && closing is null
                ? MathTableKind.Cases
                : null;
            body = AsRow(ConvertTable(children[1], inferred));
        }
        else
        {
            body = ToSlotRow(children[1]);
        }

        bool scalable = ReadBooleanAttribute(children[0], "stretchy") ||
                        ReadBooleanAttribute(children[2], "stretchy");
        node = new MathDelimiter(body, opening, closing, scalable);
        return true;
    }

    private MathRow ConvertContentRow(XElement element)
    {
        var nodes = new List<MathNode>();
        foreach (XNode child in element.Nodes())
        {
            switch (child)
            {
                case XElement childElement:
                    nodes.Add(ConvertElement(childElement));
                    break;
                case XText text when !string.IsNullOrWhiteSpace(text.Value):
                    nodes.Add(CreateError(
                        text.Value,
                        "MC3105",
                        "Text outside a MathML token element is unsupported.",
                        text));
                    break;
                case XProcessingInstruction instruction:
                    nodes.Add(CreateError(
                        instruction.ToString(SaveOptions.DisableFormatting),
                        "MC3104",
                        "A processing instruction was ignored.",
                        instruction,
                        addDiagnostic: false));
                    break;
            }
        }

        return new MathRow(nodes);
    }

    private MathRow ToSlotRow(XElement element)
    {
        MathNode node = ConvertElement(element);
        return AsRow(node);
    }

    private MathRow ConvertCellRow(XElement cell)
    {
        XElement[] elements = cell.Elements().ToArray();
        bool hasSignificantText = cell.Nodes().OfType<XText>()
            .Any(static text => !string.IsNullOrWhiteSpace(text.Value));
        return elements.Length == 1 &&
               !hasSignificantText &&
               IsMathMlElement(elements[0], "mrow")
            ? ToSlotRow(elements[0])
            : ConvertContentRow(cell);
    }

    private static MathRow AsRow(MathNode node) =>
        node as MathRow ?? new MathRow([node]);

    private MathError CreateArityError(XElement element, string message) =>
        CreateElementError(element, "MC3105", message);

    private MathError CreateElementError(XElement element, string code, string message) =>
        CreateError(element.ToString(SaveOptions.DisableFormatting), code, message, element);

    private MathError CreateError(
        string raw,
        string code,
        string message,
        XObject? source,
        bool addDiagnostic = true)
    {
        if (addDiagnostic)
        {
            AddDiagnostic(code, MathDiagnosticSeverity.Error, message, source);
        }

        return new MathError(MathTextFormat.MathMl, raw, code, message);
    }

    private void AddDiagnostic(
        string code,
        MathDiagnosticSeverity severity,
        string message,
        XObject? source)
    {
        _diagnostics.Add(new MathDiagnostic(
            code,
            severity,
            message,
            MathTextFormat.MathMl,
            GetSpan(source)));
    }

    private void ValidateXmlTree(XElement root)
    {
        int count = 0;
        var stack = new Stack<(XElement Element, int Depth)>();
        stack.Push((root, 1));
        while (stack.Count > 0)
        {
            (XElement element, int depth) = stack.Pop();
            count++;
            if (count > MathImportLimits.MaximumDocumentNodes)
            {
                throw new MathImportLimitExceededException(
                "MC3004",
                "XML element count exceeds the 50,000-node import limit.",
                GetSpan(element) ?? new MathSourceSpan(0, _source.Length));
            }

            if (depth > MathImportLimits.MaximumStructuralDepth)
            {
                throw new MathImportLimitExceededException(
                "MC3003",
                "XML depth exceeds the 256-level import limit.",
                GetSpan(element) ?? new MathSourceSpan(0, _source.Length));
            }

            foreach (XAttribute attribute in element.Attributes())
            {
                EnsureTokenLimit(attribute.Value, attribute);
            }

            foreach (XText text in element.Nodes().OfType<XText>())
            {
                EnsureTokenLimit(text.Value, text);
            }

            foreach (XElement child in element.Elements())
            {
                stack.Push((child, checked(depth + 1)));
            }
        }
    }

    private void ValidateProducedDocument(MathRow root)
    {
        int count = 0;
        var stack = new Stack<(MathNode Node, int Depth)>();
        stack.Push((root, 1));
        while (stack.Count > 0)
        {
            (MathNode node, int depth) = stack.Pop();
            count++;
            if (count > MathImportLimits.MaximumDocumentNodes)
            {
                throw new MathImportLimitExceededException(
                "MC3004",
                "Produced document exceeds the 50,000-node import limit.",
                new MathSourceSpan(0, _source.Length));
            }

            if (depth > MathImportLimits.MaximumStructuralDepth)
            {
                throw new MathImportLimitExceededException(
                "MC3003",
                "Produced document depth exceeds the 256-level import limit.",
                new MathSourceSpan(0, _source.Length));
            }

            foreach (MathNode child in GetChildren(node))
            {
                stack.Push((child, checked(depth + 1)));
            }
        }
    }

    private static IEnumerable<MathNode> GetChildren(MathNode node) => node switch
    {
        MathRow row => row.Children,
        MathFraction fraction => [fraction.Numerator, fraction.Denominator],
        MathRadical { Degree: not null } radical => [radical.Radicand, radical.Degree],
        MathRadical radical => [radical.Radicand],
        MathFunction function => function.Arguments,
        MathScript { Subscript: not null, Superscript: not null } script =>
            [script.Base, script.Subscript, script.Superscript],
        MathScript { Subscript: not null } script => [script.Base, script.Subscript],
        MathScript script => [script.Base, script.Superscript!],
        MathUnderOver { Below: not null, Above: not null } underOver =>
            [underOver.Base, underOver.Below, underOver.Above],
        MathUnderOver { Below: not null } underOver => [underOver.Base, underOver.Below],
        MathUnderOver { Above: not null } underOver => [underOver.Base, underOver.Above],
        MathUnderOver underOver => [underOver.Base],
        MathAccent accent => [accent.Base],
        MathDelimiter delimiter => [delimiter.Body],
        MathTable table => table.Rows.SelectMany(static row => row),
        _ => []
    };

    private void EnsureTokenLimit(string value, XObject source)
    {
        if (Encoding.UTF8.GetByteCount(value) > MathImportLimits.MaximumTokenUtf8Bytes)
        {
            throw new MathImportLimitExceededException(
            "MC3002",
            "A MathML token exceeds the 16 KiB UTF-8 token limit.",
            GetSpan(source) ?? new MathSourceSpan(0, _source.Length));
        }
    }

    private bool HasSecurityAttribute(XElement element)
    {
        foreach (XAttribute attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                continue;
            }

            string name = attribute.Name.LocalName;
            if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("href", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("src", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                AddDiagnostic(
                    "MC3103",
                    MathDiagnosticSeverity.Error,
                    "A script, link, style, or external-resource attribute was rejected.",
                    attribute);
                return true;
            }
        }

        return false;
    }

    private void ValidateUnknownAttributes(XElement element)
    {
        AllowedAttributes.TryGetValue(element.Name.LocalName, out HashSet<string>? allowed);
        foreach (XAttribute attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration ||
                attribute.Name.Namespace == XNamespace.Xml ||
                (allowed?.Contains(attribute.Name.LocalName) ?? false))
            {
                continue;
            }

            AddDiagnostic(
                "MC3106",
                MathDiagnosticSeverity.Warning,
                "An unsupported MathML attribute was ignored.",
                attribute);
        }
    }

    private string? ParseDelimiterValue(string value, XObject source)
    {
        EnsureTokenLimit(value, source);
        if (value.Length == 0)
        {
            return null;
        }

        return UnicodeScalarText.CountScalars(value) == 1 ? value : null;
    }

    private static bool IsDelimiterSymbol(string? value) => value is null or
        "(" or ")" or "[" or "]" or "{" or "}" or "|" or "‖" or "⌊" or "⌋" or
        "⌈" or "⌉" or "⟨" or "⟩";

    private static bool IsNaryBase(MathNode node)
    {
        MathText? text = node switch
        {
            MathText direct => direct,
            MathRow { Children.Length: 1 } row => row.Children[0] as MathText,
            _ => null
        };
        return text?.Text is "∑" or "∏" or "∐" or "∫" or "∬" or "∭";
    }

    private static bool TryGetConstruction(
        string marker,
        bool below,
        out MathUnderOverKind kind)
    {
        MathUnderOverKind? value = (marker, below) switch
        {
            ("¯", false) => MathUnderOverKind.Overbar,
            ("_", true) => MathUnderOverKind.Underbar,
            ("⏞", false) => MathUnderOverKind.Overbrace,
            ("⏟", true) => MathUnderOverKind.Underbrace,
            _ => null
        };
        kind = value ?? default;
        return value is not null;
    }

    private static bool TryGetAccent(string marker, out MathAccentKind kind)
    {
        MathAccentKind? value = marker switch
        {
            "́" or "´" => MathAccentKind.Acute,
            "̀" or "`" => MathAccentKind.Grave,
            "̂" or "^" => MathAccentKind.Hat,
            "̌" or "ˇ" => MathAccentKind.Check,
            "̆" or "˘" => MathAccentKind.Breve,
            "̃" or "~" => MathAccentKind.Tilde,
            "̄" => MathAccentKind.Bar,
            "̇" or "˙" => MathAccentKind.Dot,
            "̈" or "¨" => MathAccentKind.DoubleDot,
            "⃛" => MathAccentKind.TripleDot,
            "⃗" or "→" => MathAccentKind.Vector,
            _ => null
        };
        kind = value ?? default;
        return value is not null;
    }

    private static bool ReadBooleanAttribute(XElement element, string name) =>
        ((string?)element.Attribute(name))?.Trim() is "true" or "1";

    private static bool IsSupportedNamespace(XElement element) =>
        element.Name.NamespaceName.Length == 0 ||
        element.Name.NamespaceName == MathMlNamespace;

    private static bool IsMathMlElement(XElement element, string localName) =>
        IsSupportedNamespace(element) && element.Name.LocalName == localName;

    private MathSourceSpan? GetSpan(XObject? source)
    {
        if (source is not IXmlLineInfo lineInfo || !lineInfo.HasLineInfo())
        {
            return null;
        }

        int lineIndex = Math.Clamp(lineInfo.LineNumber - 1, 0, _lineStarts.Length - 1);
        int start = _lineStarts[lineIndex] + Math.Max(0, lineInfo.LinePosition - 1);
        start = Math.Clamp(start, 0, _source.Length);
        return new MathSourceSpan(start, Math.Min(1, _source.Length - start));
    }

    private static int[] FindLineStarts(string source)
    {
        var starts = new List<int> { 0 };
        for (int index = 0; index < source.Length; index++)
        {
            if (source[index] == '\n')
            {
                starts.Add(index + 1);
            }
        }

        return starts.ToArray();
    }

    private static HashSet<string> Set(params string[] names) =>
        new(names, StringComparer.Ordinal);
}
