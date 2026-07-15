using Graphing;
using Graphing.Renderer;
using SkiaSharp;

namespace GraphingRaster.Skia;

public sealed class PngGraphBitmap : IBitmap
{
    private readonly ReadOnlyMemory<byte> _data;

    public PngGraphBitmap(ReadOnlyMemory<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException("PNG data cannot be empty.", nameof(data));
        }

        _data = data.ToArray();
    }

    public ReadOnlyMemory<byte> GetData() => _data;
}

public static class SkiaGraphFrameRasterizer
{
    public static ReadOnlyMemory<byte> EncodePng(GraphFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        using var target = new SkiaGraphDrawingTarget();
        target.BeginFrame(frame);
        foreach (GraphFrameCommand command in frame.Commands)
        {
            target.Draw(command);
        }

        target.EndFrame();
        return target.GetPngData();
    }
}

public sealed class SkiaGraphDrawingTarget : IGraphDrawingTarget, IDisposable
{
    private SKSurface? _surface;
    private SKCanvas? _canvas;
    private SKData? _png;
    private int _clipDepth;
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

        var info = new SKImageInfo(
            pixelWidth,
            pixelHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Premul,
            SKColorSpace.CreateSrgb());
        _surface = SKSurface.Create(info);
        _canvas = _surface.Canvas;
        _canvas.Clear(ToSkia(frame.Background));
        _canvas.Scale((float)scaleX, (float)scaleY);
        _clipDepth = 0;
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
                canvas.ClipRect(ToSkia(push.Clip), SKClipOperation.Intersect, antialias: false);
                break;
            case PopClipCommand:
                if (_clipDepth <= 0)
                {
                    throw new InvalidOperationException("The graph frame contains an unbalanced clip pop.");
                }

                canvas.Restore();
                _clipDepth--;
                break;
            case StrokePathCommand stroke:
                if (stroke.Paint.LineStyle == LineStyle.Dash)
                {
                    DrawDashedPath(canvas, stroke.Path, stroke.Paint);
                }
                else
                {
                    using SKPath path = ToSkia(stroke.Path);
                    using SKPaint paint = ToStrokePaint(stroke.Paint);
                    canvas.DrawPath(path, paint);
                }

                break;
            case FillPathCommand fill:
                using (SKPath path = ToSkia(fill.Path))
                using (SKPaint paint = ToFillPaint(fill.Paint))
                {
                    canvas.DrawPath(path, paint);
                }

                break;
            case MarkerCommand marker:
                DrawMarker(canvas, marker);
                break;
            case GlyphBackgroundCommand background:
                DrawGlyphBackground(canvas, background);
                break;
            case GlyphCommand glyph:
                DrawGlyph(canvas, glyph);
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

        canvas.Flush();
        using SKImage image = _surface!.Snapshot();
        _png = image.Encode(SKEncodedImageFormat.Png, 100) ??
               throw new InvalidOperationException("Skia failed to encode the graph as PNG.");
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

    private static void DrawMarker(SKCanvas canvas, MarkerCommand marker)
    {
        using SKPaint fill = ToFillPaint(marker.Fill);
        using SKPaint? stroke = marker.Stroke is { } strokePaint ? ToStrokePaint(strokePaint) : null;
        float x = (float)marker.Center.X;
        float y = (float)marker.Center.Y;
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
                using (var path = new SKPath())
                {
                    path.MoveTo(x, y - radius);
                    path.LineTo(x + radius, y);
                    path.LineTo(x, y + radius);
                    path.LineTo(x - radius, y);
                    path.Close();
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
                canvas.DrawRect(new SKRect(
                    x - radius - halfStroke,
                    y - halfStroke,
                    x + radius + halfStroke,
                    y + halfStroke),
                    cross);
                canvas.DrawRect(new SKRect(
                    x - halfStroke,
                    y - radius - halfStroke,
                    x + halfStroke,
                    y + radius + halfStroke),
                    cross);

                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(marker));
        }
    }

    private static void DrawDashedPath(SKCanvas canvas, GraphPath path, GraphPaint graphPaint)
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
        SKPath? dash = new();
        GraphPoint current = path.Points[0];
        dash.MoveTo((float)current.X, (float)current.Y);
        bool dashHasLine = false;
        using SKPaint paint = ToStrokePaint(graphPaint with { LineStyle = LineStyle.Solid });
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
                        canvas.DrawPath(dash!, paint);
                    }

                    dash!.Dispose();
                    dash = null;
                    dashHasLine = false;
                }

                drawing = !drawing;
                remaining = interval;
                if (drawing)
                {
                    dash = new SKPath();
                    dash.MoveTo((float)current.X, (float)current.Y);
                }
            }

            current = target;
        }

        if (drawing && dashHasLine)
        {
            canvas.DrawPath(dash!, paint);
        }

        dash?.Dispose();
    }

    private static void DrawGlyph(SKCanvas canvas, GlyphCommand glyph)
    {
        using SKTypeface typeface = Typeface(glyph);
        using var font = new SKFont(typeface, glyph.FontSize);
        using SKPaint paint = ToFillPaint(glyph.Paint);
        float width = font.MeasureText(glyph.Text);
        float x = AlignedX(glyph, width);

        canvas.DrawText(glyph.Text, x, (float)glyph.Origin.Y + glyph.FontSize, font, paint);
    }

    private static void DrawGlyphBackground(SKCanvas canvas, GlyphBackgroundCommand background)
    {
        GlyphCommand glyph = background.Glyph;
        using SKTypeface typeface = Typeface(glyph);
        using var font = new SKFont(typeface, glyph.FontSize);
        float width = font.MeasureText(glyph.Text);
        SKFontMetrics metrics = font.Metrics;
        float height = metrics.Descent - metrics.Ascent + metrics.Leading;
        float x = AlignedX(glyph, width);
        using SKPaint paint = ToFillPaint(background.Paint);
        canvas.DrawRect(x, (float)glyph.Origin.Y, width, height, paint);
    }

    private static SKTypeface Typeface(GlyphCommand glyph) =>
        SKTypeface.FromFamilyName(
            glyph.FontFamily,
            glyph.FontStyle == GraphFontStyle.Italic ? SKFontStyle.Italic : SKFontStyle.Normal)
        ?? SKTypeface.Default;

    private static float AlignedX(GlyphCommand glyph, float width) => glyph.Alignment switch
    {
        GraphTextAlignment.Center => (float)glyph.Origin.X - (width * 0.5f),
        GraphTextAlignment.End => (float)glyph.Origin.X - width,
        _ => (float)glyph.Origin.X
    };

    private static SKPath ToSkia(GraphPath graphPath)
    {
        var path = new SKPath();
        if (graphPath.Points.IsEmpty)
        {
            return path;
        }

        GraphPoint first = graphPath.Points[0];
        path.MoveTo((float)first.X, (float)first.Y);
        for (int index = 1; index < graphPath.Points.Length; index++)
        {
            GraphPoint point = graphPath.Points[index];
            path.LineTo((float)point.X, (float)point.Y);
        }

        if (graphPath.IsClosed)
        {
            path.Close();
        }

        return path;
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

    private static SKPaint ToFillPaint(GraphPaint paint) => new()
    {
        Color = ToSkia(paint.Color),
        IsAntialias = paint.AntiAlias,
        Style = SKPaintStyle.Fill
    };

    private static SKColor ToSkia(Color color) => new(color.R, color.G, color.B, color.A);

    private static SKRect ToSkia(GraphRect rectangle) => new(
        (float)rectangle.X,
        (float)rectangle.Y,
        (float)rectangle.Right,
        (float)rectangle.Bottom);

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
