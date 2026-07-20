using System.Collections.Immutable;

namespace MathComposer.Avalonia.OpenType;

/// <summary>Ready-made variants and an optional repeatable construction.</summary>
public sealed record OpenTypeMathGlyphConstruction
{
    /// <summary>Initializes a construction record.</summary>
    public OpenTypeMathGlyphConstruction(
        ImmutableArray<OpenTypeMathGlyphVariant> variants,
        OpenTypeMathGlyphAssembly? assembly)
    {
        Variants = variants.IsDefault ? [] : variants;
        Assembly = assembly;
    }

    /// <summary>Gets variants in increasing extension-axis size.</summary>
    public ImmutableArray<OpenTypeMathGlyphVariant> Variants { get; }

    /// <summary>Gets the optional assembly used beyond the largest variant.</summary>
    public OpenTypeMathGlyphAssembly? Assembly { get; }
}
