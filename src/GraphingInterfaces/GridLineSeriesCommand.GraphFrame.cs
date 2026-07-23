using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;

/// <summary>
/// Describes one regularly-spaced horizontal or vertical grid-line series.
/// Keeping the series procedural avoids creating a command object for every
/// line while the viewport is changing.
/// </summary>
public sealed record GridLineSeriesCommand(
    AxisRange Range,
    double ViewportWidth,
    double ViewportHeight,
    double FirstValue,
    double ValueStep,
    double MajorStep,
    int Count,
    bool IsVertical,
    GraphPaint MajorPaint,
    GraphPaint MinorPaint) : GraphFrameCommand(GraphCommandKind.GridLineSeries)
{
    public void GetLine(int index, out GraphPoint start, out GraphPoint end, out GraphPaint paint)
    {
        double value = FirstValue + index * ValueStep;
        double quotient = value / MajorStep;
        paint = Math.Abs(quotient - Math.Round(quotient)) <= 1e-9 ? MajorPaint : MinorPaint;
        if (IsVertical)
        {
            double x = (value - Range.Minimum) / Range.Length * ViewportWidth;
            start = new GraphPoint(x, 0);
            end = new GraphPoint(x, ViewportHeight);
        }
        else
        {
            double y = (Range.Maximum - value) / Range.Length * ViewportHeight;
            start = new GraphPoint(0, y);
            end = new GraphPoint(ViewportWidth, y);
        }
    }
}
