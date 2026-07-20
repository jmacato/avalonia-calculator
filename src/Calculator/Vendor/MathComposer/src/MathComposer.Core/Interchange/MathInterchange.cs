using System.Globalization;

namespace MathComposer.Core;

/// <summary>Dispatches deterministic import and export across supported text formats.</summary>
public static class MathInterchange
{
    /// <summary>Imports text in the requested format.</summary>
    /// <param name="text">The user-provided source text.</param>
    /// <param name="format">The source interchange format.</param>
    /// <param name="culture">
    /// Optional culture used only for UnicodeMath localized punctuation aliases.
    /// </param>
    /// <returns>The recovered document and ordered diagnostics.</returns>
    public static MathParseResult Parse(
        string text,
        MathTextFormat format,
        CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        return format switch
        {
            MathTextFormat.UnicodeMath => UnicodeMathParser.Parse(text, culture),
            MathTextFormat.Latex => LatexParser.Parse(text),
            MathTextFormat.MathMl => MathMlParser.Parse(text),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    /// <summary>Exports a document in the requested deterministic format.</summary>
    /// <param name="document">The immutable document to export.</param>
    /// <param name="format">The target interchange format.</param>
    /// <returns>Canonical, culture-invariant text.</returns>
    public static string Serialize(MathDocument document, MathTextFormat format)
    {
        ArgumentNullException.ThrowIfNull(document);
        return format switch
        {
            MathTextFormat.UnicodeMath => UnicodeMathSerializer.Serialize(document),
            MathTextFormat.Latex => LatexSerializer.Serialize(document),
            MathTextFormat.MathMl => MathMlSerializer.Serialize(document),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }
}
