using System.Collections.Immutable;
using Graphing;

namespace JsMath.Port;

public readonly record struct SamplingViewport(AxisRange XRange, AxisRange YRange, double Width, double Height)
{
    public bool IsValid => XRange.IsFiniteAndOrdered && YRange.IsFiniteAndOrdered && double.IsFinite(Width) && double.IsFinite(Height) && Width > 0 && Height > 0;

    public GraphPoint ToScreen(double x, double y) => new(((x - XRange.Minimum) / XRange.Length) * Width, ((YRange.Maximum - y) / YRange.Length) * Height);
    public GraphPoint ToUser(double screenX, double screenY) => new(XRange.Minimum + ((screenX / Width) * XRange.Length), YRange.Maximum - ((screenY / Height) * YRange.Length));
    public bool ContainsWithMargin(GraphPoint point, double margin) => point.X >= -margin && point.X <= Width + margin && point.Y >= -margin && point.Y <= Height + margin;
}
