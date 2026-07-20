using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace MathComposer.Core;

/// <summary>Imports the bounded, package-free LaTeX math subset.</summary>
public static class LatexParser
{
    /// <summary>Parses safe LaTeX math without executing or expanding commands.</summary>
    /// <param name="text">LaTeX math without dollar delimiters or a preamble.</param>
    /// <returns>The recovered document and ordered diagnostics.</returns>
    public static MathParseResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Encoding.UTF8.GetByteCount(text) > MathImportLimits.MaximumInputUtf8Bytes)
        {
            return CreateFatalResult(
                text,
                "MC2001",
                "Input exceeds the 1 MiB UTF-8 import limit.",
                new MathSourceSpan(0, text.Length));
        }

        try
        {
            return new LatexParserState(text).Parse();
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
        var error = new MathError(MathTextFormat.Latex, source, code, message);
        var document = new MathDocument(new MathRow([error]));
        var diagnostic = new MathDiagnostic(
            code,
            MathDiagnosticSeverity.Fatal,
            message,
            MathTextFormat.Latex,
            span);
        return new MathParseResult(document, [diagnostic]);
    }
}
