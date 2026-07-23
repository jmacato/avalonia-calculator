namespace Graphing;
/// <summary>
/// Transforms command coordinates while leaving device-sized paint properties
/// such as stroke widths, marker radii and font sizes unchanged.
/// </summary>
public readonly record struct GraphCoordinateTransform(double ScaleX, double ScaleY, double OffsetX, double OffsetY)
{
    public GraphPoint Transform(GraphPoint point)
    {
        return new GraphPoint(point.X * ScaleX + OffsetX, point.Y * ScaleY + OffsetY);
    }
}
