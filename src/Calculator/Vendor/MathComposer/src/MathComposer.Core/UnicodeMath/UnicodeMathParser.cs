using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace MathComposer.Core;

/// <summary>Imports the supported, bounded UnicodeMath authoring profile.</summary>
public static class UnicodeMathParser
{
    /// <summary>Parses UnicodeMath using the current culture for additional punctuation aliases.</summary>
    /// <param name="text">User-authored UnicodeMath.</param>
    /// <param name="culture">The culture used only for localized import punctuation.</param>
    /// <returns>The recovered document and ordered diagnostics.</returns>
    public static MathParseResult Parse(string text, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (Encoding.UTF8.GetByteCount(text) > MathImportLimits.MaximumInputUtf8Bytes)
        {
            return CreateFatalResult(
                text,
                "MC1001",
                "Input exceeds the 1 MiB UTF-8 import limit.",
                new MathSourceSpan(0, text.Length));
        }

        try
        {
            return new UnicodeMathParserState(text, culture ?? CultureInfo.CurrentCulture).Parse();
        }
        catch (MathImportLimitExceededException exception)
        {
            return CreateFatalResult(text, exception.Code, exception.Message, exception.Span);
        }
    }

    private static MathParseResult CreateFatalResult(
        string source,
        string code,
        string message,
        MathSourceSpan span)
    {
        var error = new MathError(MathTextFormat.UnicodeMath, source, code, message);
        var document = new MathDocument(new MathRow([error]));
        var diagnostic = new MathDiagnostic(
            code,
            MathDiagnosticSeverity.Fatal,
            message,
            MathTextFormat.UnicodeMath,
            span);
        return new MathParseResult(document, [diagnostic]);
    }
}
