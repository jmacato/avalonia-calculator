using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Avalonia;
using MathComposer.Avalonia.OpenType;
using MathComposer.Core;

namespace MathComposer.Avalonia.Layout;

internal sealed class MathLayoutBox
{
    public MathLayoutBox(double width, double ascent, double descent)
    {
        Width = width;
        Ascent = Math.Max(0, ascent);
        Descent = Math.Max(0, descent);
    }

    public double Width { get; set; }

    public double Ascent { get; set; }

    public double Descent { get; set; }

    public List<MathDrawCommand> Commands { get; } = [];

    public List<MathCaretStop> CaretStops { get; } = [];

    public void Add(MathLayoutBox child, double x, double baseline)
    {
        foreach (MathDrawCommand command in child.Commands)
        {
            Commands.Add(MathLayoutEngine.TranslateCommand(command, x, baseline));
        }

        foreach (MathCaretStop stop in child.CaretStops)
        {
            CaretStops.Add(new MathCaretStop(
                stop.Position,
                stop.Bounds.Translate(new Vector(x, baseline))));
        }
    }

    public void UpdateRowCaretHeights(ImmutableArray<int> path)
    {
        for (int index = 0; index < CaretStops.Count; index++)
        {
            MathCaretStop stop = CaretStops[index];
            if (stop.Position.Path.AsSpan().SequenceEqual(path.AsSpan()))
            {
                CaretStops[index] = new MathCaretStop(
                    stop.Position,
                    new Rect(stop.Bounds.X, -Ascent, stop.Bounds.Width, Ascent + Descent));
            }
        }
    }
}
