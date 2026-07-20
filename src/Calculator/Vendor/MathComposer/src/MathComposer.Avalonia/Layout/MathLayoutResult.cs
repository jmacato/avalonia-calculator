using System.Collections.Immutable;
using Avalonia;
using MathComposer.Core;

namespace MathComposer.Avalonia.Layout;

/// <summary>An immutable laid-out formula, rendering stream, and hit-test map.</summary>
public sealed class MathLayoutResult
{
    /// <summary>Initializes a complete layout result.</summary>
    public MathLayoutResult(
        Size size,
        double baseline,
        ImmutableArray<MathDrawCommand> commands,
        ImmutableArray<MathCaretStop> caretStops,
        ImmutableArray<MathDiagnostic> diagnostics)
    {
        Size = size;
        Baseline = baseline;
        Commands = commands.IsDefault ? [] : commands;
        CaretStops = caretStops.IsDefault ? [] : caretStops;
        Diagnostics = diagnostics.IsDefault ? [] : diagnostics;
    }

    /// <summary>Gets the formula extent in device-independent pixels.</summary>
    public Size Size { get; }

    /// <summary>Gets the top-relative baseline.</summary>
    public double Baseline { get; }

    /// <summary>Gets immutable draw commands in paint order.</summary>
    public ImmutableArray<MathDrawCommand> Commands { get; }

    /// <summary>Gets legal caret stops in structural document order.</summary>
    public ImmutableArray<MathCaretStop> CaretStops { get; }

    /// <summary>Gets non-fatal layout diagnostics.</summary>
    public ImmutableArray<MathDiagnostic> Diagnostics { get; }

    /// <summary>Finds the closest legal caret stop to a pointer location.</summary>
    public MathPosition HitTest(Point point)
    {
        if (CaretStops.IsDefaultOrEmpty)
        {
            return new MathPosition([], 0);
        }

        MathCaretStop closest = CaretStops[0];
        double closestDistance = DistanceSquared(point, closest.Bounds);
        for (int index = 1; index < CaretStops.Length; index++)
        {
            MathCaretStop candidate = CaretStops[index];
            double distance = DistanceSquared(point, candidate.Bounds);
            if (distance < closestDistance)
            {
                closest = candidate;
                closestDistance = distance;
            }
        }

        return closest.Position;
    }

    /// <summary>Builds selection rectangles from the same geometry as hit testing.</summary>
    public ImmutableArray<Rect> GetSelectionRectangles(
        MathDocument document,
        MathSelection selection)
    {
        ArgumentNullException.ThrowIfNull(document);
        MathSelection normalized = MathSelectionServices.Normalize(document, selection).Selection;
        if (normalized.IsCollapsed)
        {
            return [];
        }

        MathPosition start;
        MathPosition end;
        if (MathSelectionServices.Compare(document, normalized.Anchor, normalized.Active) <= 0)
        {
            start = normalized.Anchor;
            end = normalized.Active;
        }
        else
        {
            start = normalized.Active;
            end = normalized.Anchor;
        }

        MathCaretStop? startStop = FindStop(start);
        MathCaretStop? endStop = FindStop(end);
        if (startStop is null || endStop is null)
        {
            return [];
        }

        Rect first = startStop.Value.Bounds;
        Rect last = endStop.Value.Bounds;
        double left = Math.Min(first.Center.X, last.Center.X);
        double right = Math.Max(first.Center.X, last.Center.X);
        double top = Math.Min(first.Top, last.Top);
        double bottom = Math.Max(first.Bottom, last.Bottom);
        if (right - left < 1)
        {
            right = left + 1;
        }

        return [new Rect(left, top, right - left, Math.Max(1, bottom - top))];
    }

    private MathCaretStop? FindStop(MathPosition position)
    {
        foreach (MathCaretStop stop in CaretStops)
        {
            if (stop.Position == position)
            {
                return stop;
            }
        }

        return null;
    }

    private static double DistanceSquared(Point point, Rect bounds)
    {
        double x = Math.Clamp(point.X, bounds.Left, bounds.Right);
        double y = Math.Clamp(point.Y, bounds.Top, bounds.Bottom);
        double dx = point.X - x;
        double dy = point.Y - y;
        return dx * dx + dy * dy;
    }
}
