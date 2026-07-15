using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Graphing;
using Graphing.Renderer;
using AvaloniaColor = Avalonia.Media.Color;

namespace GraphControl;

internal sealed class AvaloniaGraphDrawingTarget(DrawingContext context) : IGraphDrawingTarget, IDisposable
{
    private readonly Stack<IDisposable> _clips = new();

    public void BeginFrame(GraphFrame frame)
    {
        context.DrawRectangle(
            Brush(frame.Background),
            null,
            new Rect(0, 0, frame.Width, frame.Height));
    }

    public void Draw(GraphFrameCommand command)
    {
        switch (command)
        {
            case PushClipCommand push:
                _clips.Push(context.PushClip(ToRect(push.Clip)));
                break;
            case PopClipCommand:
                if (_clips.Count == 0)
                {
                    throw new InvalidOperationException("The graph frame contains an unbalanced clip pop.");
                }

                _clips.Pop().Dispose();
                break;
            case StrokePathCommand stroke:
                if (stroke.Paint.LineStyle == LineStyle.Dash)
                {
                    DrawDashedPath(stroke.Path, stroke.Paint);
                }
                else
                {
                    context.DrawGeometry(null, Pen(stroke.Paint), Geometry(stroke.Path));
                }

                break;
            case FillPathCommand fill:
                context.DrawGeometry(Brush(fill.Paint.Color), null, Geometry(fill.Path));
                break;
            case MarkerCommand marker:
                DrawMarker(marker);
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
        while (_clips.Count > 0)
        {
            _clips.Pop().Dispose();
        }
    }

    public void Dispose() => EndFrame();

    private void DrawDashedPath(GraphPath path, GraphPaint graphPaint)
    {
        if (path.Points.Length < 2)
        {
            return;
        }

        // Direct2D's captured dash values are 2,2 in stroke-width units.
        // Segmenting here avoids backend-specific dash scaling and phase.
        double interval = Math.Max(1, graphPaint.StrokeWidth) * 2;
        double remaining = interval;
        bool drawing = true;
        List<GraphPoint>? dash = [path.Points[0]];
        GraphPoint current = path.Points[0];
        Pen paint = Pen(graphPaint with { LineStyle = LineStyle.Solid });
        int segmentCount = path.IsClosed ? path.Points.Length : path.Points.Length - 1;
        for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            GraphPoint target = path.Points[(segmentIndex + 1) % path.Points.Length];
            double dx = target.X - current.X;
            double dy = target.Y - current.Y;
            double segmentRemaining = Math.Sqrt((dx * dx) + (dy * dy));
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
                var next = new GraphPoint(
                    current.X + (unitX * amount),
                    current.Y + (unitY * amount));
                if (drawing)
                {
                    dash!.Add(next);
                }

                current = next;
                segmentRemaining -= amount;
                remaining -= amount;
                if (remaining > 1e-12)
                {
                    continue;
                }

                if (drawing && dash!.Count >= 2)
                {
                    context.DrawGeometry(null, paint, Geometry(new GraphPath(dash)));
                }

                drawing = !drawing;
                remaining = interval;
                dash = drawing ? [current] : null;
            }

            current = target;
        }

        if (drawing && dash is { Count: >= 2 })
        {
            context.DrawGeometry(null, paint, Geometry(new GraphPath(dash)));
        }
    }

    private void DrawMarker(MarkerCommand marker)
    {
        IBrush fill = Brush(marker.Fill.Color);
        Pen? stroke = marker.Stroke is { } strokePaint ? Pen(strokePaint) : null;
        var center = new Point(marker.Center.X, marker.Center.Y);
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
                context.DrawGeometry(fill, stroke, Geometry(new GraphPath(
                [
                    new GraphPoint(center.X, center.Y - radius),
                    new GraphPoint(center.X + radius, center.Y),
                    new GraphPoint(center.X, center.Y + radius),
                    new GraphPoint(center.X - radius, center.Y)
                ], true)));
                break;
            case GraphMarkerShape.Cross:
            {
                GraphPaint crossPaint = marker.Stroke ?? marker.Fill;
                IBrush cross = Brush(crossPaint.Color);
                double halfStroke = crossPaint.StrokeWidth * 0.5;
                context.DrawRectangle(
                    cross,
                    null,
                    new Rect(
                        center.X - radius - halfStroke,
                        center.Y - halfStroke,
                        (radius + halfStroke) * 2,
                        halfStroke * 2));
                context.DrawRectangle(
                    cross,
                    null,
                    new Rect(
                        center.X - halfStroke,
                        center.Y - radius - halfStroke,
                        halfStroke * 2,
                        (radius + halfStroke) * 2));
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(marker));
        }
    }

    private void DrawGlyph(GlyphCommand glyph)
    {
        FormattedText text = Format(glyph, Brush(glyph.Paint.Color));
        double x = AlignedX(glyph, text.Width);
        context.DrawText(text, new Point(x, glyph.Origin.Y));
    }

    private void DrawGlyphBackground(GlyphBackgroundCommand background)
    {
        FormattedText text = Format(background.Glyph, Brush(background.Paint.Color));
        double x = AlignedX(background.Glyph, text.Width);
        context.DrawRectangle(
            Brush(background.Paint.Color),
            null,
            new Rect(x, background.Glyph.Origin.Y, text.Width, text.Height));
    }

    private static FormattedText Format(GlyphCommand glyph, IBrush brush) => new(
        glyph.Text,
        CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight,
        new Typeface(
            glyph.FontFamily,
            glyph.FontStyle == GraphFontStyle.Italic ? FontStyle.Italic : FontStyle.Normal),
        glyph.FontSize,
        brush);

    private static double AlignedX(GlyphCommand glyph, double width) => glyph.Alignment switch
    {
        GraphTextAlignment.Center => glyph.Origin.X - (width * 0.5),
        GraphTextAlignment.End => glyph.Origin.X - width,
        _ => glyph.Origin.X
    };

    private static StreamGeometry Geometry(GraphPath path)
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

    private static Pen Pen(GraphPaint paint)
    {
        IDashStyle? dash = paint.LineStyle switch
        {
            LineStyle.Dot => DashStyle.Dot,
            LineStyle.Dash => new DashStyle([2, 2], 0),
            LineStyle.DashDot => DashStyle.DashDot,
            LineStyle.DashDotDot => DashStyle.DashDotDot,
            _ => null
        };
        return new Pen(
            Brush(paint.Color),
            paint.StrokeWidth,
            dash,
            paint.LineStyle == LineStyle.Dot ? PenLineCap.Round : PenLineCap.Flat,
            PenLineJoin.Round,
            10);
    }

    private static SolidColorBrush Brush(Graphing.Color color) => new(
        AvaloniaColor.FromArgb(color.A, color.R, color.G, color.B));

    private static Point ToPoint(GraphPoint point) => new(point.X, point.Y);

    private static Rect ToRect(GraphRect rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
}
