using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Graphing;
using Graphing.Renderer;
using AvaloniaColor = Avalonia.Media.Color;

namespace GraphControl;

internal sealed class AvaloniaGraphRenderCache
{
    private const int MaximumBrushes = 32;
    private const int MaximumPens = 64;
    private const int MaximumTextLayouts = 128;
    private const int MaximumGeometries = 256;
    private const int MaximumGeometryPoints = 65_536;
    private const int MaximumHatchGeometries = 8;
    private static readonly ImmutableDashStyle DotDashStyle = ToImmutable(DashStyle.Dot);
    private static readonly ImmutableDashStyle DashDashStyle = ToImmutable(DashStyle.Dash);
    private static readonly ImmutableDashStyle DashDotDashStyle = ToImmutable(DashStyle.DashDot);
    private static readonly ImmutableDashStyle DashDotDotDashStyle = ToImmutable(DashStyle.DashDotDot);
    private readonly Dictionary<GraphPath, StreamGeometry> _geometries =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(GraphPath Path, float Width), StreamGeometry> _dashedGeometries = [];
    private readonly Dictionary<HatchGridCommand, StreamGeometry> _hatchGeometries =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Graphing.Color, ImmutableSolidColorBrush> _brushes = [];
    private readonly Dictionary<GraphPaint, ImmutablePen> _pens = [];
    private readonly Dictionary<TextLayoutKey, TextLayout> _textLayouts = [];
    private GraphFrame? _settledFrame;
    private GraphPath? _lastPath;
    private StreamGeometry? _lastGeometry;
    private (GraphPath Path, float Width)? _lastDashedPath;
    private StreamGeometry? _lastDashedGeometry;
    private int _cachedGeometryPoints;
    private int _cachedHatchGeometryPoints;

    public void BeginFrame(GraphFrame frame)
    {
        if (frame.IsStale || ReferenceEquals(frame, _settledFrame))
        {
            return;
        }

        _settledFrame = frame;
        _geometries.Clear();
        _dashedGeometries.Clear();
        _hatchGeometries.Clear();
        _lastPath = null;
        _lastGeometry = null;
        _lastDashedPath = null;
        _lastDashedGeometry = null;
        _cachedGeometryPoints = 0;
        _cachedHatchGeometryPoints = 0;
    }

    public StreamGeometry Geometry(GraphPath path)
    {
        if (path.Points.Length < 8)
        {
            return AvaloniaGraphDrawingTarget.CreateGeometry(path);
        }

        if (ReferenceEquals(path, _lastPath))
        {
            return _lastGeometry!;
        }

        if (!_geometries.TryGetValue(path, out StreamGeometry? geometry))
        {
            geometry = AvaloniaGraphDrawingTarget.CreateGeometry(path);
            CacheGeometry(path, geometry);
        }

        _lastPath = path;
        _lastGeometry = geometry;
        return geometry;
    }

    public StreamGeometry DashedGeometry(GraphPath path, GraphPaint paint)
    {
        var key = (path, paint.StrokeWidth);
        if (_lastDashedPath is { } last &&
            ReferenceEquals(last.Path, path) &&
            last.Width == paint.StrokeWidth)
        {
            return _lastDashedGeometry!;
        }

        if (!_dashedGeometries.TryGetValue(key, out StreamGeometry? geometry))
        {
            geometry = AvaloniaGraphDrawingTarget.CreateDashedGeometry(path, paint.StrokeWidth);
            if (CanCache(path))
            {
                _dashedGeometries.Add(key, geometry);
                _cachedGeometryPoints += path.Points.Length;
            }
        }

        _lastDashedPath = key;
        _lastDashedGeometry = geometry;
        return geometry;
    }

    public ImmutablePen Pen(GraphPaint paint)
    {
        if (_pens.TryGetValue(paint, out ImmutablePen? pen))
        {
            return pen;
        }

        if (_pens.Count >= MaximumPens)
        {
            _pens.Clear();
        }

        ImmutableDashStyle? dash = paint.LineStyle switch
        {
            LineStyle.Dot => DotDashStyle,
            LineStyle.Dash => DashDashStyle,
            LineStyle.DashDot => DashDotDashStyle,
            LineStyle.DashDotDot => DashDotDotDashStyle,
            _ => null
        };
        pen = new ImmutablePen(
            Brush(paint.Color),
            paint.StrokeWidth,
            dash,
            paint.LineStyle == LineStyle.Dot ? PenLineCap.Round : PenLineCap.Flat,
            PenLineJoin.Round,
            10);
        _pens.Add(paint, pen);
        return pen;
    }

    public StreamGeometry HatchGeometry(HatchGridCommand hatch)
    {
        if (_hatchGeometries.TryGetValue(hatch, out StreamGeometry? geometry))
        {
            return geometry;
        }

        geometry = AvaloniaGraphDrawingTarget.CreateHatchGeometry(hatch, out int pointCount);
        if (_hatchGeometries.Count < MaximumHatchGeometries &&
            pointCount <= MaximumGeometryPoints - _cachedGeometryPoints - _cachedHatchGeometryPoints)
        {
            _hatchGeometries.Add(hatch, geometry);
            _cachedHatchGeometryPoints += pointCount;
        }

        return geometry;
    }

    public ImmutableSolidColorBrush Brush(Graphing.Color color)
    {
        if (_brushes.TryGetValue(color, out ImmutableSolidColorBrush? brush))
        {
            return brush;
        }

        if (_brushes.Count >= MaximumBrushes)
        {
            _brushes.Clear();
        }

        brush = new ImmutableSolidColorBrush(AvaloniaColor.FromArgb(color.A, color.R, color.G, color.B));
        _brushes.Add(color, brush);
        return brush;
    }

    public TextLayout Format(GlyphCommand glyph)
    {
        var key = new TextLayoutKey(
            glyph.Text,
            glyph.FontFamily,
            glyph.FontSize,
            glyph.FontStyle,
            glyph.Paint.Color);
        if (_textLayouts.TryGetValue(key, out TextLayout? layout))
        {
            return layout;
        }

        if (_textLayouts.Count >= MaximumTextLayouts)
        {
            ClearTextLayouts();
        }

        layout = new TextLayout(
            glyph.Text,
            new Typeface(
                glyph.FontFamily,
                glyph.FontStyle == GraphFontStyle.Italic ? FontStyle.Italic : FontStyle.Normal),
            glyph.FontSize,
            Brush(glyph.Paint.Color));
        _textLayouts.Add(key, layout);
        return layout;
    }

    public void Clear()
    {
        _settledFrame = null;
        _geometries.Clear();
        _dashedGeometries.Clear();
        _hatchGeometries.Clear();
        _brushes.Clear();
        _pens.Clear();
        _lastPath = null;
        _lastGeometry = null;
        _lastDashedPath = null;
        _lastDashedGeometry = null;
        _cachedGeometryPoints = 0;
        _cachedHatchGeometryPoints = 0;
        ClearTextLayouts();
    }

    private void CacheGeometry(GraphPath path, StreamGeometry geometry)
    {
        if (!CanCache(path))
        {
            return;
        }

        _geometries.Add(path, geometry);
        _cachedGeometryPoints += path.Points.Length;
    }

    private bool CanCache(GraphPath path) =>
        _geometries.Count + _dashedGeometries.Count + _hatchGeometries.Count < MaximumGeometries &&
        path.Points.Length <= MaximumGeometryPoints - _cachedGeometryPoints - _cachedHatchGeometryPoints;

    private void ClearTextLayouts()
    {
        foreach (TextLayout layout in _textLayouts.Values)
        {
            layout.Dispose();
        }

        _textLayouts.Clear();
    }

    private static ImmutableDashStyle ToImmutable(IDashStyle dashStyle) =>
        new(dashStyle.Dashes, dashStyle.Offset);

    private readonly record struct TextLayoutKey(
        string Text,
        string FontFamily,
        float FontSize,
        GraphFontStyle FontStyle,
        Graphing.Color Color);
}

internal sealed class AvaloniaGraphDrawingTarget : IGraphDrawingTarget, IDisposable
{
    private readonly DrawingContext _context;
    private readonly AvaloniaGraphRenderCache _cache;
    private readonly Stack<DrawingContext.PushedState> _clips = new();
    private GraphCoordinateTransform? _coordinateTransform;

    public AvaloniaGraphDrawingTarget(DrawingContext context, AvaloniaGraphRenderCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public void BeginFrame(GraphFrame frame)
    {
        _cache.BeginFrame(frame);
        _context.DrawRectangle(
            Brush(frame.Background),
            null,
            new Rect(0, 0, frame.Width, frame.Height));
    }

    public void Draw(GraphFrameCommand command)
    {
        switch (command)
        {
            case PushClipCommand push:
                _clips.Push(_context.PushClip(ToRect(Transform(push.Clip))));
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
            case StrokePathCommand stroke:
                if (stroke.Paint.LineStyle == LineStyle.Dash)
                {
                    DrawDashedPath(stroke.Path, stroke.Paint);
                }
                else if (stroke.Path is { IsClosed: false, Points.Length: 2 })
                {
                    _context.DrawLine(
                        Pen(stroke.Paint),
                        ToPoint(Transform(stroke.Path.Points[0])),
                        ToPoint(Transform(stroke.Path.Points[1])));
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

    public void Dispose() => EndFrame();

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
        _context.DrawGeometry(
            null,
            Pen(graphPaint with { LineStyle = LineStyle.Solid }),
            _cache.DashedGeometry(path, graphPaint));
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
                _context.DrawEllipse(fill, stroke, center, radius, radius);
                break;
            case GraphMarkerShape.Square:
                _context.DrawRectangle(fill, stroke, new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2));
                break;
            case GraphMarkerShape.Diamond:
                _context.DrawGeometry(fill, stroke, CreateGeometry(new GraphPath(
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
                _context.DrawRectangle(
                    cross,
                    null,
                    new Rect(
                        center.X - radius - halfStroke,
                        center.Y - halfStroke,
                        (radius + halfStroke) * 2,
                        halfStroke * 2));
                _context.DrawRectangle(
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
            _context.DrawGeometry(brush, null, _cache.HatchGeometry(hatch));
            return;
        }

        double halfStroke = hatch.Paint.StrokeWidth * 0.5;
        double radius = hatch.Radius;
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
                GraphPoint center = Transform(new GraphPoint(x, y));
                _context.DrawRectangle(
                    brush,
                    null,
                    new Rect(
                        center.X - radius - halfStroke,
                        center.Y - halfStroke,
                        (radius + halfStroke) * 2,
                        halfStroke * 2));
                _context.DrawRectangle(
                    brush,
                    null,
                    new Rect(
                        center.X - halfStroke,
                        center.Y - radius - halfStroke,
                        halfStroke * 2,
                        (radius + halfStroke) * 2));
            }
        }
    }

    private void DrawGlyph(GlyphCommand glyph)
    {
        TextLayout text = Format(glyph);
        GraphPoint origin = Transform(glyph.Origin);
        double x = AlignedX(glyph.Alignment, origin.X, text.Width);
        text.Draw(_context, new Point(x, origin.Y));
    }

    private void DrawGlyphBackground(GlyphBackgroundCommand background)
    {
        TextLayout text = Format(background.Glyph);
        GraphPoint origin = Transform(background.Glyph.Origin);
        double x = AlignedX(background.Glyph.Alignment, origin.X, text.Width);
        _context.DrawRectangle(
            Brush(background.Paint.Color),
            null,
            new Rect(x, origin.Y, text.Width, text.Height));
    }

    private void DrawGeometry(IBrush? fill, GraphPaint? stroke, GraphPath path)
    {
        if (_coordinateTransform is not { } transform)
        {
            _context.DrawGeometry(fill, stroke is { } directPaint ? Pen(directPaint) : null, _cache.Geometry(path));
            return;
        }

        using DrawingContext.PushedState state = _context.PushTransform(new Matrix(
            transform.ScaleX,
            0,
            0,
            transform.ScaleY,
            transform.OffsetX,
            transform.OffsetY));
        GraphPaint? adjusted = stroke is { } graphPaint
            ? graphPaint with { StrokeWidth = (float)(graphPaint.StrokeWidth / TransformStrokeScale(transform)) }
            : null;
        _context.DrawGeometry(fill, adjusted is { } transformedPaint ? Pen(transformedPaint) : null, _cache.Geometry(path));
    }

    private TextLayout Format(GlyphCommand glyph) => _cache.Format(glyph);

    private static double AlignedX(GraphTextAlignment alignment, double originX, double width) => alignment switch
    {
        GraphTextAlignment.Center => originX - (width * 0.5),
        GraphTextAlignment.End => originX - width,
        _ => originX
    };

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
                AppendRectangle(
                    writer,
                    x - radius - halfStroke,
                    y - halfStroke,
                    (radius + halfStroke) * 2,
                    halfStroke * 2);
                AppendRectangle(
                    writer,
                    x - halfStroke,
                    y - radius - halfStroke,
                    halfStroke * 2,
                    (radius + halfStroke) * 2);
                pointCount += 8;
            }
        }

        return geometry;
    }

    private static void AppendRectangle(
        StreamGeometryContext writer,
        double x,
        double y,
        double width,
        double height)
    {
        writer.BeginFigure(new Point(x, y), isFilled: true);
        writer.LineTo(new Point(x + width, y));
        writer.LineTo(new Point(x + width, y + height));
        writer.LineTo(new Point(x, y + height));
        writer.EndFigure(isClosed: true);
    }

    private GraphPoint Transform(GraphPoint point) =>
        _coordinateTransform?.Transform(point) ?? point;

    private GraphRect Transform(GraphRect rectangle)
    {
        if (_coordinateTransform is not { } transform)
        {
            return rectangle;
        }

        GraphPoint topLeft = transform.Transform(new GraphPoint(rectangle.X, rectangle.Y));
        GraphPoint bottomRight = transform.Transform(new GraphPoint(rectangle.Right, rectangle.Bottom));
        return new GraphRect(
            Math.Min(topLeft.X, bottomRight.X),
            Math.Min(topLeft.Y, bottomRight.Y),
            Math.Abs(bottomRight.X - topLeft.X),
            Math.Abs(bottomRight.Y - topLeft.Y));
    }

    private static double TransformStrokeScale(GraphCoordinateTransform transform) =>
        Math.Sqrt(Math.Abs(transform.ScaleX * transform.ScaleY));

    private ImmutablePen Pen(GraphPaint paint) => _cache.Pen(paint);

    private ImmutableSolidColorBrush Brush(Graphing.Color color) => _cache.Brush(color);

    private static Point ToPoint(GraphPoint point) => new(point.X, point.Y);

    private static Rect ToRect(GraphRect rectangle) => new(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
}
