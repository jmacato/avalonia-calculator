using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Graphing;
using Graphing.Renderer;
using AvaloniaColor = Avalonia.Media.Color;

namespace GraphControl;

public enum EquationLineStyle
{
    Solid = 0,
    Dot = 1,
    Dash = 2,
    DashDot = 3,
    DashDotDot = 4
}
