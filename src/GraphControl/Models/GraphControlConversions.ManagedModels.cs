using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Graphing;
using Graphing.Renderer;
using AvaloniaColor = Avalonia.Media.Color;

namespace GraphControl;

internal static class GraphControlConversions
{
    public static Graphing.Color ToGraphColor(this AvaloniaColor color) => new(color.R, color.G, color.B, color.A);
    public static LineStyle ToGraphLineStyle(this EquationLineStyle style) => (LineStyle)(int)style;
}
