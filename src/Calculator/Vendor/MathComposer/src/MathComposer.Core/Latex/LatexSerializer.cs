using System.Text;

namespace MathComposer.Core;

/// <summary>Produces deterministic package-free LaTeX math.</summary>
public static class LatexSerializer
{
    /// <summary>Exports a document using the supported safe LaTeX subset.</summary>
    /// <param name="document">The immutable document to export.</param>
    /// <returns>Canonical LaTeX without a preamble or dollar delimiters.</returns>
    public static string Serialize(MathDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var writer = new LatexSerializerWriter();
        writer.WriteRow(document.Root);
        return writer.ToString();
    }
}
