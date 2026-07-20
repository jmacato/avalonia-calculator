using System.Collections.Immutable;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace MathComposer.Core;

/// <summary>Imports the bounded, safe Presentation MathML subset.</summary>
public static class MathMlParser
{
    /// <summary>Parses Presentation MathML without resolving or executing external content.</summary>
    /// <param name="text">A complete XML document rooted at <c>math</c>.</param>
    /// <returns>The recovered document and ordered diagnostics.</returns>
    public static MathParseResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Encoding.UTF8.GetByteCount(text) > MathImportLimits.MaximumInputUtf8Bytes)
        {
            return CreateFatalResult(
                text,
                "MC3001",
                "Input exceeds the 1 MiB UTF-8 import limit.",
                new MathSourceSpan(0, text.Length));
        }

        try
        {
            return new MathMlImporter(text).Parse();
        }
        catch (MathImportLimitExceededException exception)
        {
            return CreateFatalResult(text, exception.Code, exception.Message, exception.Span);
        }
        catch (XmlException exception)
        {
            int start = Math.Clamp(exception.LinePosition - 1, 0, text.Length);
            return CreateRecoveredErrorResult(
                text,
                "MC3101",
                "The XML is malformed or contains a prohibited DTD.",
                new MathSourceSpan(start, Math.Min(1, text.Length - start)));
        }

        static MathParseResult CreateRecoveredErrorResult(
            string source,
            string code,
            string message,
            MathSourceSpan span)
        {
            var error = new MathError(MathTextFormat.MathMl, source, code, message);
            var diagnostic = new MathDiagnostic(
                code,
                MathDiagnosticSeverity.Error,
                message,
                MathTextFormat.MathMl,
                span);
            return new MathParseResult(new MathDocument(new MathRow([error])), [diagnostic]);
        }
    }

    private static MathParseResult CreateFatalResult(
        string source,
        string code,
        string message,
        MathSourceSpan span)
    {
        var error = new MathError(MathTextFormat.MathMl, source, code, message);
        var diagnostic = new MathDiagnostic(
            code,
            MathDiagnosticSeverity.Fatal,
            message,
            MathTextFormat.MathMl,
            span);
        return new MathParseResult(new MathDocument(new MathRow([error])), [diagnostic]);
    }
}
