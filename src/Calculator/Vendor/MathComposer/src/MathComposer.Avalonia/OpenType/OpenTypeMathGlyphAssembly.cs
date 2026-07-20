using System.Collections.Immutable;

namespace MathComposer.Avalonia.OpenType;

/// <summary>A repeatable glyph assembly.</summary>
public sealed record OpenTypeMathGlyphAssembly
{
    /// <summary>Initializes an assembly record.</summary>
    public OpenTypeMathGlyphAssembly(
        short italicsCorrection,
        ImmutableArray<OpenTypeMathGlyphPart> parts)
    {
        ItalicsCorrection = italicsCorrection;
        Parts = parts.IsDefault ? [] : parts;
    }

    /// <summary>Gets the assembly italics correction.</summary>
    public short ItalicsCorrection { get; }

    /// <summary>Gets ordered parts, bottom-to-top or left-to-right.</summary>
    public ImmutableArray<OpenTypeMathGlyphPart> Parts { get; }
}
