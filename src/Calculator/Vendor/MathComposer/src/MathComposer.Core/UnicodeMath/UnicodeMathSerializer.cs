namespace MathComposer.Core;

/// <summary>Produces deterministic canonical UnicodeMath text.</summary>
public static class UnicodeMathSerializer
{
    /// <summary>Exports a document as canonical, culture-invariant UnicodeMath.</summary>
    /// <param name="document">The immutable document to export.</param>
    /// <returns>Canonical UnicodeMath text.</returns>
    public static string Serialize(MathDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var writer = new UnicodeMathSerializerWriter();
        writer.WriteRow(document.Root, parentPrecedence: 0);
        return writer.ToString();
    }
}
