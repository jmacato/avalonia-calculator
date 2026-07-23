using Graphing;
using Graphing.Renderer;
using SkiaSharp;

namespace GraphingRaster.Skia;

public sealed class SkiaGraphDrawingTarget : IGraphDrawingTarget, IDisposable
{
    private SKSurface? _surface;
    private SKCanvas? _canvas;
    private SKData? _png;
    private int _clipDepth;
    private GraphCoordinateTransform? _coordinateTransform;
    private bool _ended;
    public void BeginFrame(GraphFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        DisposeFrame();
        if (frame.Width == 0 || frame.Height == 0 || frame.Width > int.MaxValue || frame.Height > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }

        double scaleX = frame.DpiX / 96.0;
        double scaleY = frame.DpiY / 96.0;
        int pixelWidth = checked((int)Math.Round(frame.Width * scaleX, MidpointRounding.AwayFromZero));
        int pixelHeight = checked((int)Math.Round(frame.Height * scaleY, MidpointRounding.AwayFromZero));
        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }

        using SKColorSpace colorSpace = SKColorSpace.CreateSrgb();
        var info = new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Rgba8888, SKAlphaType.Premul, colorSpace);
        _surface = SKSurface.Create(info);
        _canvas = _surface.Canvas;
        _canvas.Clear(ToSkia(frame.Background));
        _canvas.Scale((float)scaleX, (float)scaleY);
        _clipDepth = 0;
        _coordinateTransform = null;
        _ended = false;
    }

    public void Draw(GraphFrameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        SKCanvas canvas = _canvas ?? throw new InvalidOperationException("BeginFrame must be called before drawing.");
        if (_ended)
        {
            throw new InvalidOperationException("The frame has already ended.");
        }

        switch (command)
        {
            case PushClipCommand push:
                canvas.Save();
                _clipDepth++;
                canvas.ClipRect(ToSkia(Transform(push.Clip, _coordinateTransform)), SKClipOperation.Intersect, antialias: false);
                break;
            case PopClipCommand:
                if (_clipDepth <= 0)
                {
                    throw new InvalidOperationException("The graph frame contains an unbalanced clip pop.");
                }

                canvas.Restore();
                _clipDepth--;
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
                using (SKPaint paint = ToStrokePaint(line.Paint))
                {
                    GraphPoint start = Transform(line.Start, _coordinateTransform);
                    GraphPoint end = Transform(line.End, _coordinateTransform);
                    canvas.DrawLine((float)start.X, (float)start.Y, (float)end.X, (float)end.Y, paint);
                }

                break;
            case GridLineSeriesCommand series:
                DrawGridLineSeries(canvas, series, _coordinateTransform);
                break;
            case StrokePathCommand stroke:
                if (stroke.Paint.LineStyle == LineStyle.Dash)
                {
                    DrawDashedPath(canvas, stroke.Path, stroke.Paint, _coordinateTransform);
                }
                else
                {
                    using SKPath path = ToSkia(stroke.Path, _coordinateTransform);
                    using SKPaint paint = ToStrokePaint(stroke.Paint);
                    canvas.DrawPath(path, paint);
                }

                break;
            case FillPathCommand fill:
                using (SKPath path = ToSkia(fill.Path, _coordinateTransform))
                using (SKPaint paint = ToFillPaint(fill.Paint))
                {
                    canvas.DrawPath(path, paint);
                }

                break;
            case MarkerCommand marker:
                DrawMarker(canvas, marker, _coordinateTransform);
                break;
            case HatchGridCommand hatch:
                DrawHatchGrid(canvas, hatch, _coordinateTransform);
                break;
            case GlyphBackgroundCommand background:
                DrawGlyphBackground(canvas, background, _coordinateTransform);
                break;
            case GlyphCommand glyph:
                DrawGlyph(canvas, glyph, _coordinateTransform);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    public void EndFrame()
    {
        SKCanvas canvas = _canvas ?? throw new InvalidOperationException("BeginFrame must be called before EndFrame.");
        while (_clipDepth > 0)
        {
            canvas.Restore();
            _clipDepth--;
        }

        _coordinateTransform = null;
        canvas.Flush();
        using SKImage image = _surface!.Snapshot();
        _png = image.Encode(SKEncodedImageFormat.Png, 100) ?? throw new InvalidOperationException("Skia failed to encode the graph as PNG.");
        _ended = true;
    }

    public ReadOnlyMemory<byte> GetPngData()
    {
        if (!_ended || _png is null)
        {
            throw new InvalidOperationException("EndFrame must be called before reading PNG data.");
        }

        return _png.ToArray();
    }

    public void Dispose()
    {
        DisposeFrame();
        GC.SuppressFinalize(this);
    }

    private static void DrawGridLineSeries(SKCanvas canvas, GridLineSeriesCommand series, GraphCoordinateTransform? transform)
    {
        using SKPaint majorPaint = ToStrokePaint(series.MajorPaint);
        using SKPaint minorPaint = ToStrokePaint(series.MinorPaint);
        for (int index = 0; index < series.Count; index++)
        {
            series.GetLine(index, out GraphPoint start, out GraphPoint end, out GraphPaint paint);
            start = Transform(start, transform);
            end = Transform(end, transform);
            SKPaint skiaPaint = paint == series.MajorPaint ? majorPaint : minorPaint;
            canvas.DrawLine((float)start.X, (float)start.Y, (float)end.X, (float)end.Y, skiaPaint);
        }
    }

    private static void DrawMarker(SKCanvas canvas, MarkerCommand marker, GraphCoordinateTransform? transform)
    {
        using SKPaint fill = ToFillPaint(marker.Fill);
        using SKPaint? stroke = marker.Stroke is { } strokePaint ? ToStrokePaint(strokePaint) : null;
        GraphPoint center = Transform(marker.Center, transform);
        float x = (float)center.X;
        float y = (float)center.Y;
        float radius = marker.Radius;
        switch (marker.Shape)
        {
            case GraphMarkerShape.Circle:
                canvas.DrawCircle(x, y, radius, fill);
                if (stroke is not null)
                {
                    canvas.DrawCircle(x, y, radius, stroke);
                }

                break;
            case GraphMarkerShape.Square:
                {
                    var rectangle = new SKRect(x - radius, y - radius, x + radius, y + radius);
                    canvas.DrawRect(rectangle, fill);
                    if (stroke is not null)
                    {
                        canvas.DrawRect(rectangle, stroke);
                    }

                    break;
                }

            case GraphMarkerShape.Diamond:
                using (var builder = new SKPathBuilder())
                {
                    builder.MoveTo(x, y - radius);
                    builder.LineTo(x + radius, y);
                    builder.LineTo(x, y + radius);
                    builder.LineTo(x - radius, y);
                    builder.Close();
                    using SKPath path = builder.Detach();
                    canvas.DrawPath(path, fill);
                    if (stroke is not null)
                    {
                        canvas.DrawPath(path, stroke);
                    }
                }

                break;
            case GraphMarkerShape.Cross:
                {
                    GraphPaint crossPaint = marker.Stroke ?? marker.Fill;
                    float halfStroke = crossPaint.StrokeWidth * 0.5f;
                    using SKPaint cross = ToFillPaint(crossPaint);
                    canvas.DrawRect(new SKRect(x - radius - halfStroke, y - halfStroke, x + radius + halfStroke, y + halfStroke), cross);
                    canvas.DrawRect(new SKRect(x - halfStroke, y - radius - halfStroke, x + halfStroke, y + radius + halfStroke), cross);
                    break;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(marker));
        }
    }

    private static void DrawHatchGrid(SKCanvas canvas, HatchGridCommand hatch, GraphCoordinateTransform? transform)
    {
        if (hatch.LatticeIntervals < 2 || hatch.Occupancy.IsDefaultOrEmpty)
        {
            return;
        }

        using SKPaint paint = ToFillPaint(hatch.Paint);
        float halfStroke = hatch.Paint.StrokeWidth * 0.5f;
        float radius = hatch.Radius;
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
                GraphPoint center = Transform(new GraphPoint(x, y), transform);
                float centerX = (float)center.X;
                float centerY = (float)center.Y;
                canvas.DrawRect(new SKRect(centerX - radius - halfStroke, centerY - halfStroke, centerX + radius + halfStroke, centerY + halfStroke), paint);
                canvas.DrawRect(new SKRect(centerX - halfStroke, centerY - radius - halfStroke, centerX + halfStroke, centerY + radius + halfStroke), paint);
            }
        }
    }

    private static void DrawDashedPath(SKCanvas canvas, GraphPath path, GraphPaint graphPaint, GraphCoordinateTransform? transform)
    {
        if (path.Points.Length < 2)
        {
            return;
        }

        // Direct2D's captured dash values are 2,2 in stroke-width units.
        // Segmenting here keeps Euclidean cadence independent of Skia's CTM.
        double interval = Math.Max(1, graphPaint.StrokeWidth) * 2;
        double remaining = interval;
        bool drawing = true;
        SKPathBuilder? dash = new();
        GraphPoint current = Transform(path.Points[0], transform);
        dash.MoveTo((float)current.X, (float)current.Y);
        bool dashHasLine = false;
        using SKPaint paint = ToStrokePaint(graphPaint with { LineStyle = LineStyle.Solid });
        int segmentCount = path.IsClosed ? path.Points.Length : path.Points.Length - 1;
        for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            GraphPoint target = Transform(path.Points[(segmentIndex + 1) % path.Points.Length], transform);
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
                    dash!.LineTo((float)next.X, (float)next.Y);
                    dashHasLine = true;
                }

                current = next;
                segmentRemaining -= amount;
                remaining -= amount;
                if (remaining > 1e-12)
                {
                    continue;
                }

                if (drawing)
                {
                    if (dashHasLine)
                    {
                        using SKPath dashPath = dash!.Detach();
                        canvas.DrawPath(dashPath, paint);
                    }

                    dash!.Dispose();
                    dash = null;
                    dashHasLine = false;
                }

                drawing = !drawing;
                remaining = interval;
                if (drawing)
                {
                    dash = new SKPathBuilder();
                    dash.MoveTo((float)current.X, (float)current.Y);
                }
            }

            current = target;
        }

        if (drawing && dashHasLine)
        {
            using SKPath dashPath = dash!.Detach();
            canvas.DrawPath(dashPath, paint);
        }

        dash?.Dispose();
    }

    private static void DrawGlyph(SKCanvas canvas, GlyphCommand glyph, GraphCoordinateTransform? transform)
    {
        using var fontLease = SkiaGraphFontCache.Rent(glyph.FontFamily, glyph.FontStyle, glyph.FontSize);
        var font = fontLease.Font;
        using SKPaint paint = ToFillPaint(glyph.Paint);
        float width = font.MeasureText(glyph.Text);
        GraphPoint origin = Transform(glyph.Origin, transform);
        float x = AlignedX(glyph.Alignment, (float)origin.X, width);
        canvas.DrawText(
            glyph.Text,
            x,
            (float)origin.Y + glyph.FontSize,
            SKTextAlign.Left,
            font,
            paint);
    }

    private static void DrawGlyphBackground(SKCanvas canvas, GlyphBackgroundCommand background, GraphCoordinateTransform? transform)
    {
        GlyphCommand glyph = background.Glyph;
        using var fontLease = SkiaGraphFontCache.Rent(glyph.FontFamily, glyph.FontStyle, glyph.FontSize);
        var font = fontLease.Font;
        float width = font.MeasureText(glyph.Text);
        SKFontMetrics metrics = font.Metrics;
        float height = metrics.Descent - metrics.Ascent + metrics.Leading;
        GraphPoint origin = Transform(glyph.Origin, transform);
        float x = AlignedX(glyph.Alignment, (float)origin.X, width);
        using SKPaint paint = ToFillPaint(background.Paint);
        canvas.DrawRect(x, (float)origin.Y, width, height, paint);
    }

    private static float AlignedX(GraphTextAlignment alignment, float originX, float width)
    {
        return alignment switch
        {
            GraphTextAlignment.Center => originX - width * 0.5f,
            GraphTextAlignment.End => originX - width,
            _ => originX
        };
    }

    private static SKPath ToSkia(GraphPath graphPath, GraphCoordinateTransform? transform)
    {
        using var builder = new SKPathBuilder();
        if (graphPath.Points.IsEmpty)
        {
            return builder.Detach();
        }

        GraphPoint first = Transform(graphPath.Points[0], transform);
        builder.MoveTo((float)first.X, (float)first.Y);
        for (int index = 1; index < graphPath.Points.Length; index++)
        {
            GraphPoint point = Transform(graphPath.Points[index], transform);
            builder.LineTo((float)point.X, (float)point.Y);
        }

        if (graphPath.IsClosed)
        {
            builder.Close();
        }

        return builder.Detach();
    }

    private static GraphPoint Transform(GraphPoint point, GraphCoordinateTransform? transform)
    {
        return transform?.Transform(point) ?? point;
    }

    private static GraphRect Transform(GraphRect rectangle, GraphCoordinateTransform? transform)
    {
        if (transform is not { } coordinateTransform)
        {
            return rectangle;
        }

        GraphPoint topLeft = coordinateTransform.Transform(new GraphPoint(rectangle.X, rectangle.Y));
        GraphPoint bottomRight = coordinateTransform.Transform(new GraphPoint(rectangle.Right, rectangle.Bottom));
        return new GraphRect(Math.Min(topLeft.X, bottomRight.X), Math.Min(topLeft.Y, bottomRight.Y), Math.Abs(bottomRight.X - topLeft.X), Math.Abs(bottomRight.Y - topLeft.Y));
    }

    private static SKPaint ToStrokePaint(GraphPaint paint)
    {
        var result = new SKPaint
        {
            Color = ToSkia(paint.Color),
            IsAntialias = paint.AntiAlias,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = paint.StrokeWidth,
            StrokeCap = paint.LineStyle == LineStyle.Dot ? SKStrokeCap.Round : SKStrokeCap.Butt,
            StrokeJoin = SKStrokeJoin.Round
        };
        float width = Math.Max(1, paint.StrokeWidth);
        result.PathEffect = paint.LineStyle switch
        {
            LineStyle.Dot => SKPathEffect.CreateDash([width, width * 2], 0),
            LineStyle.Dash => SKPathEffect.CreateDash([width * 2, width * 2], 0),
            LineStyle.DashDot => SKPathEffect.CreateDash([width * 4, width * 2, width, width * 2], 0),
            LineStyle.DashDotDot => SKPathEffect.CreateDash([width * 4, width * 2, width, width * 2, width, width * 2], 0),
            _ => null
        };
        return result;
    }

    private static SKPaint ToFillPaint(GraphPaint paint)
    {
        return new SKPaint { Color = ToSkia(paint.Color), IsAntialias = paint.AntiAlias, Style = SKPaintStyle.Fill };
    }

    private static SKColor ToSkia(Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }

    private static SKRect ToSkia(GraphRect rectangle)
    {
        return new SKRect((float)rectangle.X, (float)rectangle.Y, (float)rectangle.Right, (float)rectangle.Bottom);
    }

    private void DisposeFrame()
    {
        _png?.Dispose();
        _png = null;
        _surface?.Dispose();
        _surface = null;
        _canvas = null;
        _clipDepth = 0;
        _ended = false;
    }
}
