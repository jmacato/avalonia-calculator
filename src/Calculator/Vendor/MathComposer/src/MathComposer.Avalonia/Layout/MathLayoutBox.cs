using System.Collections.Immutable;
using Avalonia;

namespace MathComposer.Avalonia.Layout;

internal sealed class MathLayoutBox(double width, double ascent, double descent)
{
    public double Width { get; set; } = width;

    public double Ascent { get; set; } = Math.Max(0, ascent);

    public double Descent { get; set; } = Math.Max(0, descent);

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
