namespace Graphing;
/// <summary>RGBA color packed exactly as native <c>0xRRGGBBAA</c>.</summary>
public readonly record struct Color(byte R, byte G, byte B, byte A)
{
    public Color(byte red, byte green, byte blue) : this(red, green, blue, byte.MaxValue)
    {
    }

    public Color(uint packedValue) : this((byte)(packedValue >> 24), (byte)(packedValue >> 16), (byte)(packedValue >> 8), (byte)packedValue)
    {
    }

    public uint PackedValue => ((uint)R << 24) | ((uint)G << 16) | ((uint)B << 8) | A;

    public uint ToUInt32() => PackedValue;

    public static Color FromUInt32(uint packedValue) => new(packedValue);

    public static explicit operator uint(Color color) => color.ToUInt32();

    public static explicit operator Color(uint packedValue) => FromUInt32(packedValue);
}
