using Graphing.Renderer;
using AvaloniaColor = Avalonia.Media.Color;
using Color = Graphing.Color;

namespace GraphControl;

internal static class GraphControlConversions
{
    public static Graphing.Color ToGraphColor(this AvaloniaColor color)
    {
        return new Color(color.R, color.G, color.B, color.A);
    }

    public static LineStyle ToGraphLineStyle(this EquationLineStyle style)
    {
        return (LineStyle)(int)style;
    }
}
