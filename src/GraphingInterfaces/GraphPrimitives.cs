namespace Graphing;

/// <summary>RGBA color packed exactly as native <c>0xRRGGBBAA</c>.</summary>
public readonly record struct Color(byte R, byte G, byte B, byte A)
{
    public Color(byte red, byte green, byte blue)
        : this(red, green, blue, byte.MaxValue)
    {
    }

    public Color(uint packedValue)
        : this(
            (byte)(packedValue >> 24),
            (byte)(packedValue >> 16),
            (byte)(packedValue >> 8),
            (byte)packedValue)
    {
    }

    public uint PackedValue => ((uint)R << 24) | ((uint)G << 16) | ((uint)B << 8) | A;

    public static explicit operator uint(Color color) => color.PackedValue;

    public static explicit operator Color(uint packedValue) => new(packedValue);
}

public readonly record struct AxisRange(double Minimum, double Maximum)
{
    public double Length => Maximum - Minimum;

    public double Center => Minimum + (Length / 2d);

    public bool IsFiniteAndOrdered =>
        double.IsFinite(Minimum) && double.IsFinite(Maximum) && Minimum < Maximum;
}

public readonly record struct GraphPoint(double X, double Y)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);
}

public readonly record struct GraphRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

public readonly record struct ClosestPointData(
    int FormulaId,
    float ScreenX,
    float ScreenY,
    double X,
    double Y,
    double Rho,
    double Theta,
    double T)
{
    public static ClosestPointData Unavailable => new(
        -1,
        float.NaN,
        float.NaN,
        double.NaN,
        double.NaN,
        double.NaN,
        double.NaN,
        double.NaN);
}
