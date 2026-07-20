using System.Globalization;
using System.Xml.Linq;

namespace MathComposer.Core;

/// <summary>Produces deterministic Presentation MathML Core markup.</summary>
public static class MathMlSerializer
{
    /// <summary>The MathML namespace emitted by the serializer.</summary>
    public const string NamespaceUri = "http://www.w3.org/1998/Math/MathML";

    private static readonly XNamespace MathMl = NamespaceUri;

    /// <summary>Exports a document as canonical Presentation MathML.</summary>
    /// <param name="document">The immutable document to export.</param>
    /// <returns>A namespace-qualified <c>math</c> element without an XML declaration.</returns>
    public static string Serialize(MathDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var root = new XElement(MathMl + "math", document.Root.Children.Select(WriteNode));
        return root.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement WriteNode(MathNode node) => node switch
    {
        MathRow row => WriteRow(row),
        MathText text => new XElement(MathMl + TokenName(text.AtomClass), text.Text),
        MathFraction fraction =>
            new(MathMl + "mfrac", WriteRow(fraction.Numerator), WriteRow(fraction.Denominator)),
        MathRadical { Degree: null } radical =>
            new(MathMl + "msqrt", radical.Radicand.Children.Select(WriteNode)),
        MathRadical radical =>
            new(MathMl + "mroot", WriteRow(radical.Radicand), WriteRow(radical.Degree!)),
        MathFunction function => WriteFunction(function),
        MathScript script => WriteScript(script),
        MathUnderOver underOver => WriteUnderOver(underOver),
        MathAccent accent => WriteAccent(accent),
        MathDelimiter delimiter => WriteDelimiter(delimiter),
        MathTable table => WriteTable(table),
        MathSpacing spacing => new XElement(
            MathMl + "mspace",
            new XAttribute("width", SpacingWidth(spacing.Width))),
        MathError error => new XElement(
            MathMl + "merror",
            new XElement(MathMl + "mtext", error.RawFragment)),
        _ => throw new ArgumentException(
            $"Unsupported node type {node.GetType().FullName}.",
            nameof(node))
    };

    private static XElement WriteRow(MathRow row) =>
        new(MathMl + "mrow", row.Children.Select(WriteNode));

    private static XElement WriteFunction(MathFunction function)
    {
        XElement functionBase;
        MathRow argument;
        if (function is { Name: "log", Arguments.Length: 2 })
        {
            functionBase = new XElement(
                MathMl + "msub",
                new XElement(MathMl + "mi", "log"),
                WriteRow(function.Arguments[0]));
            argument = function.Arguments[1];
        }
        else
        {
            functionBase = new XElement(MathMl + "mi", function.Name);
            argument = function.Arguments[0];
        }

        return new XElement(
            MathMl + "mrow",
            functionBase,
            new XElement(MathMl + "mo", "⁡"),
            new XElement(
                MathMl + "mrow",
                new XElement(MathMl + "mo", "("),
                WriteRow(argument),
                new XElement(MathMl + "mo", ")")));
    }

    private static XElement WriteScript(MathScript script)
    {
        XElement @base = WriteNode(script.Base);
        return (script.Subscript, script.Superscript) switch
        {
            (not null, not null) => new XElement(
                MathMl + "msubsup",
                @base,
                WriteRow(script.Subscript),
                WriteRow(script.Superscript)),
            (not null, null) => new XElement(MathMl + "msub", @base, WriteRow(script.Subscript)),
            (null, not null) => new XElement(MathMl + "msup", @base, WriteRow(script.Superscript)),
            _ => throw new InvalidOperationException("A script must have at least one script row.")
        };
    }

    private static XElement WriteUnderOver(MathUnderOver underOver)
    {
        if (underOver.Kind == MathUnderOverKind.NaryLimits)
        {
            return WriteUnderOverElement(
                WriteRow(underOver.Base),
                underOver.Below,
                underOver.Above);
        }

        bool constructionIsBelow = underOver.Kind is
            MathUnderOverKind.Underbar or MathUnderOverKind.Underbrace;
        var marker = new XElement(MathMl + "mo", ConstructionMarker(underOver.Kind));
        XElement construction = constructionIsBelow
            ? new XElement(
                MathMl + "munder",
                new XAttribute("accentunder", "true"),
                WriteRow(underOver.Base),
                marker)
            : new XElement(
                MathMl + "mover",
                new XAttribute("accent", "true"),
                WriteRow(underOver.Base),
                marker);

        return WriteUnderOverElement(construction, underOver.Below, underOver.Above);
    }

    private static XElement WriteUnderOverElement(
        XElement @base,
        MathRow? below,
        MathRow? above) => (below, above) switch
        {
            (not null, not null) => new XElement(
                MathMl + "munderover",
                @base,
                WriteRow(below),
                WriteRow(above)),
            (not null, null) => new XElement(MathMl + "munder", @base, WriteRow(below)),
            (null, not null) => new XElement(MathMl + "mover", @base, WriteRow(above)),
            _ => @base
        };

    private static XElement WriteAccent(MathAccent accent)
    {
        string elementName = accent.Placement == MathAccentPlacement.Over ? "mover" : "munder";
        string attributeName = accent.Placement == MathAccentPlacement.Over ? "accent" : "accentunder";
        return new XElement(
            MathMl + elementName,
            new XAttribute(attributeName, "true"),
            WriteRow(accent.Base),
            new XElement(MathMl + "mo", AccentMarker(accent.Kind)));
    }

    private static XElement WriteDelimiter(MathDelimiter delimiter) =>
        new(
            MathMl + "mrow",
            WriteDelimiterOperator(delimiter.Opening, delimiter.Scalable),
            WriteRow(delimiter.Body),
            WriteDelimiterOperator(delimiter.Closing, delimiter.Scalable));

    private static XElement WriteDelimiterOperator(string? value, bool scalable)
    {
        var element = new XElement(MathMl + "mo");
        if (scalable)
        {
            element.Add(new XAttribute("stretchy", "true"));
        }

        if (value is not null)
        {
            element.Add(value);
        }

        return element;
    }

    private static XElement WriteTable(MathTable table) =>
        new(
            MathMl + "mtable",
            new XAttribute(
                "data-math-composer-layout",
                table.Kind switch
                {
                    MathTableKind.Matrix => "matrix",
                    MathTableKind.Cases => "cases",
                    MathTableKind.Aligned => "aligned",
                    MathTableKind.Gathered => "gathered",
                    _ => throw new ArgumentOutOfRangeException(nameof(table))
                }),
            table.Rows.Select(row =>
                new XElement(
                    MathMl + "mtr",
                    row.Select(cell => new XElement(MathMl + "mtd", WriteRow(cell))))));

    private static string TokenName(MathAtomClass atomClass) => atomClass switch
    {
        MathAtomClass.Identifier => "mi",
        MathAtomClass.Number => "mn",
        MathAtomClass.OrdinaryText => "mtext",
        MathAtomClass.Operator or MathAtomClass.Relation or MathAtomClass.Punctuation => "mo",
        _ => throw new ArgumentOutOfRangeException(nameof(atomClass))
    };

    private static string SpacingWidth(MathSpacingWidth width) => width switch
    {
        MathSpacingWidth.Thin => (3d / 18d).ToString("0.######", CultureInfo.InvariantCulture) + "em",
        MathSpacingWidth.Medium => (4d / 18d).ToString("0.######", CultureInfo.InvariantCulture) + "em",
        MathSpacingWidth.Em => "1em",
        _ => throw new ArgumentOutOfRangeException(nameof(width))
    };

    private static string ConstructionMarker(MathUnderOverKind kind) => kind switch
    {
        MathUnderOverKind.Overbar => "¯",
        MathUnderOverKind.Underbar => "_",
        MathUnderOverKind.Overbrace => "⏞",
        MathUnderOverKind.Underbrace => "⏟",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string AccentMarker(MathAccentKind kind) => kind switch
    {
        MathAccentKind.Acute => "́",
        MathAccentKind.Grave => "̀",
        MathAccentKind.Hat => "̂",
        MathAccentKind.Check => "̌",
        MathAccentKind.Breve => "̆",
        MathAccentKind.Tilde => "̃",
        MathAccentKind.Bar => "̄",
        MathAccentKind.Dot => "̇",
        MathAccentKind.DoubleDot => "̈",
        MathAccentKind.TripleDot => "⃛",
        MathAccentKind.Vector => "⃗",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
