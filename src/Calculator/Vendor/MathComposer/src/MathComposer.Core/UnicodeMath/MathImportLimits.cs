namespace MathComposer.Core;

/// <summary>Common resource limits applied by all Math Composer importers.</summary>
public static class MathImportLimits
{
    /// <summary>The maximum UTF-8 input size.</summary>
    public const int MaximumInputUtf8Bytes = 1024 * 1024;

    /// <summary>The maximum number of document nodes produced by an import.</summary>
    public const int MaximumDocumentNodes = 50_000;

    /// <summary>The maximum structural nesting depth.</summary>
    public const int MaximumStructuralDepth = 256;

    /// <summary>The maximum UTF-8 size of one token.</summary>
    public const int MaximumTokenUtf8Bytes = 16 * 1024;
}
