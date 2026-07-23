using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Graphing;
using Graphing.Renderer;

namespace GraphControl;

internal sealed class AvaloniaGraphDrawingTarget(DrawingContext context, AvaloniaGraphRenderCache cache)
    : IGraphDrawingTarget, IDisposable
{
    private readonly Stack<DrawingContext.PushedState> _clips = new();
    private GraphCoordinateTransform? _coordinateTransform;

    public void BeginFrame(GraphFrame frame)
    {
        cache.BeginFrame(frame);
        context.DrawRectangle(Brush(frame.Background), null, new Rect(0, 0, frame.Width, frame.Height));
    }

    public void Draw(GraphFrameCommand command)
    {
        switch (command)
        {
            case PushClipCommand push:
                _clips.Push(context.PushClip(ToRect(Transform(push.Clip))));
                break;
            case PopClipCommand:
                if (_clips.Count == 0)
                {
                    throw new InvalidOperationException("The graph frame contains an unbalanced clip pop.");
                }

                _clips.Pop().Dispose();
                break;
            case PushCoordinateTransformCommand push:
                if (_coordinateTransform is not null)
                {
                    throw new InvalidOperationException("Nested graph coordinate transforms are not supported.");
                }

                _coordinateTransform = push.Transform;
                break;
            case PopCoordinateTransformCommand:
                if (_coordinateTransform is null)
                {
                    throw new InvalidOperationException("The graph frame contains an unbalanced coordinate transform pop.");
                }

                _coordinateTransform = null;
                break;
            case CommandGroupCommand group:
                foreach (GraphFrameCommand child in group.Commands)
                {
                    Draw(child);
                }

                break;
            case StrokeLineCommand line:
                context.DrawLine(Pen(line.Paint), ToPoint(Transform(line.Start)), ToPoint(Transform(line.End)));
                break;
            case GridLineSeriesCommand series:
                DrawGridLineSeries(series);
                break;
            case StrokePathCommand stroke:
                if (stroke.Paint.LineStyle == LineStyle.Dash)
                {
                    DrawDashedPath(stroke.Path, stroke.Paint);
                }
                else if (stroke.Path is { IsClosed: false, Points.Length: 2 })
                {
                    context.DrawLine(Pen(stroke.Paint), ToPoint(Transform(stroke.Path.Points[0])), ToPoint(Transform(stroke.Path.Points[1])));
                }
                else
                {
                    DrawGeometry(null, stroke.Paint, stroke.Path);
                }

                break;
            case FillPathCommand fill:
                DrawGeometry(Brush(fill.Paint.Color), null, fill.Path);
                break;
            case MarkerCommand marker:
                DrawMarker(marker);
                break;
            case HatchGridCommand hatch:
                DrawHatchGrid(hatch);
                break;
            case GlyphBackgroundCommand background:
                DrawGlyphBackground(background);
                break;
            case GlyphCommand glyph:
                DrawGlyph(glyph);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    public void EndFrame()
    {
        _coordinateTransform = null;
        while (_clips.Count > 0)
        {
            _clips.Pop().Dispose();
        }
    }

    public void Dispose()
    {
        EndFrame();
    }

    private void DrawGridLineSeries(GridLineSeriesCommand series)
    {
        for (int index = 0; index < series.Count; index++)
        {
            series.GetLine(index, out GraphPoint start, out GraphPoint end, out GraphPaint paint);
            context.DrawLine(Pen(paint), ToPoint(Transform(start)), ToPoint(Transform(end)));
        }
    }

    private void DrawDashedPath(GraphPath path, GraphPaint graphPaint)
    {
        if (path.Points.Length < 2)
        {
            return;
        }

        if (_coordinateTransform is not null)
        {
            DrawGeometry(null, graphPaint, path);
            return;
        }

        // Direct2D's captured dash values are 2,2 in stroke-width units. A
        // single cached multi-figure geometry preserves that cadence without
        // allocating a List, GraphPath and StreamGeometry for every dash.
        context.DrawGeometry(null, Pen(graphPaint with { LineStyle = LineStyle.Solid }), cache.DashedGeometry(path, graphPaint));
    }

    private void DrawMarker(MarkerCommand marker)
    {
        IBrush fill = Brush(marker.Fill.Color);
        ImmutablePen? stroke = marker.Stroke is { } strokePaint ? Pen(strokePaint) : null;
        Point center = ToPoint(Transform(marker.Center));
        double radius = marker.Radius;
        switch (marker.Shape)
        {
            case GraphMarkerShape.Circle:
                context.DrawEllipse(fill, stroke, center, radius, radius);
                break;
            case GraphMarkerShape.Square:
                context.DrawRectangle(fill, stroke, new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));
                break;
            case GraphMarkerShape.Diamond:
                context.DrawGeometry(fill, stroke, CreateGeometry(new GraphPath([new GraphPoint(center.X, center.Y - radius), new GraphPoint(center.X + radius, center.Y), new GraphPoint(center.X, center.Y + radius), new GraphPoint(center.X - radius, center.Y)], true)));
                break;
            case GraphMarkerShape.Cross:
                {
                    GraphPaint crossPaint = marker.Stroke ?? marker.Fill;
                    IBrush cross = Brush(crossPaint.Color);
                    double halfStroke = crossPaint.StrokeWidth * 0.5;
                    context.DrawRectangle(cross, null, new Rect(center.X - radius - halfStroke, center.Y - halfStroke, (radius + halfStroke) * 2, halfStroke * 2));
                    context.DrawRectangle(cross, null, new Rect(center.X - halfStroke, center.Y - radius - halfStroke, halfStroke * 2, (radius + halfStroke) * 2));
                    break;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(marker));
        }
    }

    private void DrawHatchGrid(HatchGridCommand hatch)
    {
        if (hatch.LatticeIntervals < 2 || hatch.Occupancy.IsDefaultOrEmpty)
        {
            return;
        }

        IBrush brush = Brush(hatch.Paint.Color);
        if (_coordinateTransform is null)
        {
            // A dense inequality can contain more than 3,000 crosses. Retain
            // them as one filled geometry so Avalonia records one scene draw
            // operation rather than two rectangles per occupied lattice cell.
            context.DrawGeometry(brush, null, cache.HatchGeometry(hatch));
            return;
        }

        // Replay the retained hatch mesh under one matrix. Expanding thousands
        // of crosses into rectangles on every interaction frame was both the
        // hottest inequality path and a large transient-allocation source.
        GraphCoordinateTransform transform = _coordinateTransform.Value;
        using DrawingContext.PushedState state = context.PushTransform(new Matrix(transform.ScaleX, 0, 0, transform.ScaleY, transform.OffsetX, transform.OffsetY));
        context.DrawGeometry(brush, null, cache.HatchGeometry(hatch));
    }

    private void DrawGlyph(GlyphCommand glyph)
    {
        TextLayout text = Format(glyph);
        GraphPoint origin = Transform(glyph.Origin);
        double x = AlignedX(glyph.Alignment, origin.X, text.Width);
        text.Draw(context, new Point(x, origin.Y));
    }

    private void DrawGlyphBackground(GlyphBackgroundCommand background)
    {
        TextLayout text = Format(background.Glyph);
        GraphPoint origin = Transform(background.Glyph.Origin);
        double x = AlignedX(background.Glyph.Alignment, origin.X, text.Width);
        context.DrawRectangle(Brush(background.Paint.Color), null, new Rect(x, origin.Y, text.Width, text.Height));
    }

    private void DrawGeometry(IBrush? fill, GraphPaint? stroke, GraphPath path)
    {
        if (_coordinateTransform is not { } transform)
        {
            context.DrawGeometry(fill, stroke is { } directPaint ? Pen(directPaint) : null, cache.Geometry(path));
            return;
        }

        using DrawingContext.PushedState state = context.PushTransform(new Matrix(transform.ScaleX, 0, 0, transform.ScaleY, transform.OffsetX, transform.OffsetY));
        GraphPaint? adjusted = stroke is { } graphPaint ? graphPaint with
        {
            StrokeWidth = (float)(graphPaint.StrokeWidth / TransformStrokeScale(transform))
        }

        : null;
        context.DrawGeometry(fill, adjusted is { } transformedPaint ? Pen(transformedPaint) : null, cache.Geometry(path));
    }

    private TextLayout Format(GlyphCommand glyph)
    {
        return cache.Format(glyph);
    }

    private static double AlignedX(GraphTextAlignment alignment, double originX, double width)
    {
        return alignment switch
        {
            GraphTextAlignment.Center => originX - width * 0.5,
            GraphTextAlignment.End => originX - width,
            _ => originX
        };
    }

    internal static StreamGeometry CreateGeometry(GraphPath path)
    {
        var geometry = new StreamGeometry();
        if (path.Points.IsEmpty)
        {
            return geometry;
        }

        using StreamGeometryContext writer = geometry.Open();
        writer.BeginFigure(ToPoint(path.Points[0]), path.IsClosed);
        for (int index = 1; index < path.Points.Length; index++)
        {
            writer.LineTo(ToPoint(path.Points[index]));
        }

        writer.EndFigure(path.IsClosed);
        return geometry;
    }

    internal static StreamGeometry CreateDashedGeometry(GraphPath path, float strokeWidth)
    {
        var geometry = new StreamGeometry();
        if (path.Points.Length < 2)
        {
            return geometry;
        }

        double interval = Math.Max(1, strokeWidth) * 2;
        double remaining = interval;
        bool drawing = true;
        bool figureOpen = true;
        GraphPoint current = path.Points[0];
        using StreamGeometryContext writer = geometry.Open();
        writer.BeginFigure(ToPoint(current), isFilled: false);
        int segmentCount = path.IsClosed ? path.Points.Length : path.Points.Length - 1;
        for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            GraphPoint target = path.Points[(segmentIndex + 1) % path.Points.Length];
            double dx = target.X - current.X;
            double dy = target.Y - current.Y;
            double segmentRemaining = Math.Sqrt(dx * dx + dy * dy);
            if (segmentRemaining <= 1e-12)
            {
                current = target;
                continue;
            }

            double unitX = dx / segmentRemaining;
            double unitY = dy / segmentRemaining;
            while (segmentRemaining > 1e-12)
            {
                double amount = Math.Min(segmentRemaining, remaining);
                var next = new GraphPoint(current.X + unitX * amount, current.Y + unitY * amount);
                if (drawing)
                {
                    writer.LineTo(ToPoint(next));
                }

                current = next;
                segmentRemaining -= amount;
                remaining -= amount;
                if (remaining > 1e-12)
                {
                    continue;
                }

                if (drawing && figureOpen)
                {
                    writer.EndFigure(isClosed: false);
                    figureOpen = false;
                }

                drawing = !drawing;
                remaining = interval;
                if (drawing)
                {
                    writer.BeginFigure(ToPoint(current), isFilled: false);
                    figureOpen = true;
                }
            }

            current = target;
        }

        if (figureOpen)
        {
            writer.EndFigure(isClosed: false);
        }

        return geometry;
    }

    internal static StreamGeometry CreateHatchGeometry(HatchGridCommand hatch, out int pointCount)
    {
        var geometry = new StreamGeometry();
        pointCount = 0;
        if (hatch.LatticeIntervals < 2 || hatch.Occupancy.IsDefaultOrEmpty)
        {
            return geometry;
        }

        double halfStroke = hatch.Paint.StrokeWidth * 0.5;
        double radius = hatch.Radius;
        using StreamGeometryContext writer = geometry.Open();
        for (int column = 0; column < hatch.LatticeIntervals; column++)
        {
            double x = Math.Floor(column * hatch.Width / hatch.LatticeIntervals);
            for (int row = 1; row < hatch.LatticeIntervals; row++)
            {
                if (!hatch.IsOccupied(column, row))
                {
                    continue;
                }

                double y = Math.Floor(row * hatch.Height / hatch.LatticeIntervals);
                AppendRectangle(writer, x - radius - halfStroke, y - halfStroke, (radius + halfStroke) * 2, halfStroke * 2);
                AppendRectangle(writer, x - halfStroke, y - radius - halfStroke, halfStroke * 2, (radius + halfStroke) * 2);
                pointCount += 8;
            }
        }

        return geometry;
    }

    private static void AppendRectangle(StreamGeometryContext writer, double x, double y, double width, double height)
    {
        writer.BeginFigure(new Point(x, y), isFilled: true);
        writer.LineTo(new Point(x + width, y));
        writer.LineTo(new Point(x + width, y + height));
        writer.LineTo(new Point(x, y + height));
        writer.EndFigure(isClosed: true);
    }

    private GraphPoint Transform(GraphPoint point)
    {
        return _coordinateTransform?.Transform(point) ?? point;
    }

    private GraphRect Transform(GraphRect rectangle)
    {
        if (_coordinateTransform is not { } transform)
        {
            return rectangle;
        }

        GraphPoint topLeft = transform.Transform(new GraphPoint(rectangle.X, rectangle.Y));
        GraphPoint bottomRight = transform.Transform(new GraphPoint(rectangle.Right, rectangle.Bottom));
        return new GraphRect(Math.Min(topLeft.X, bottomRight.X), Math.Min(topLeft.Y, bottomRight.Y), Math.Abs(bottomRight.X - topLeft.X), Math.Abs(bottomRight.Y - topLeft.Y));
    }

    private static double TransformStrokeScale(GraphCoordinateTransform transform)
    {
        return Math.Sqrt(Math.Abs(transform.ScaleX * transform.ScaleY));
    }

    private ImmutablePen Pen(GraphPaint paint)
    {
        return cache.Pen(paint);
    }

    private ImmutableSolidColorBrush Brush(Graphing.Color color)
    {
        return cache.Brush(color);
    }

    private static Point ToPoint(GraphPoint point)
    {
        return new Point(point.X, point.Y);
    }

    private static Rect ToRect(GraphRect rectangle)
    {
        return new Rect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }
}
