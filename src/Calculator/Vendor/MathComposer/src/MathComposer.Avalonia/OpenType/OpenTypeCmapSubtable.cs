namespace MathComposer.Avalonia.OpenType;

internal abstract record OpenTypeCmapSubtable
{
    public abstract ushort GetGlyphId(uint scalar, OpenTypeMathFont font);
}
