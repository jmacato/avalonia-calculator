using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;

public readonly record struct GraphPaint(Color Color, float StrokeWidth = 1f, LineStyle LineStyle = LineStyle.Solid, bool AntiAlias = true);
