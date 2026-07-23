using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using System.Collections.Immutable;
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
    private static readonly FontFamily GraphFontFamily = new("Segoe UI");
    private static readonly Typeface GraphTypeface = new(GraphFontFamily);
    private static readonly Typeface ItalicGraphTypeface = new(GraphFontFamily, FontStyle.Italic);
    private readonly Dictionary<GraphPath, StreamGeometry> _geometries = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(GraphPath Path, float Width), StreamGeometry> _dashedGeometries = [];
    private readonly Dictionary<HatchGridCommand, StreamGeometry> _hatchGeometries = new(ReferenceEqualityComparer.Instance);
    private readonly (Graphing.Color Key, ImmutableSolidColorBrush? Value)[] _brushes = new (Graphing.Color, ImmutableSolidColorBrush?)[MaximumBrushes];
    private readonly (GraphPaint Key, ImmutablePen? Value)[] _pens = new (GraphPaint, ImmutablePen?)[MaximumPens];
    private readonly (AvaloniaGraphRenderCacheTextLayoutKey Key, TextLayout? Value)[] _textLayouts = new (AvaloniaGraphRenderCacheTextLayoutKey, TextLayout?)[MaximumTextLayouts];
    private GraphFrame? _settledFrame;
    private ImmutableArray<GraphFrameCommand> _projectedEquationCommands;
    private GraphPath? _lastPath;
    private StreamGeometry? _lastGeometry;
    private (GraphPath Path, float Width)? _lastDashedPath;
    private StreamGeometry? _lastDashedGeometry;
    private int _cachedGeometryPoints;
    private int _cachedHatchGeometryPoints;
    private int _brushCount;
    private int _penCount;
    private int _textLayoutCount;
    public void BeginFrame(GraphFrame frame)
    {
        if (ReferenceEquals(frame, _settledFrame))
        {
            return;
        }

        ImmutableArray<GraphFrameCommand> projectedEquationCommands = FindEquationCommands(frame);
        if (!projectedEquationCommands.IsDefault)
        {
            if (projectedEquationCommands == _projectedEquationCommands)
            {
                return;
            }

            _projectedEquationCommands = projectedEquationCommands;
            ClearGeometryCaches();
            return;
        }

        _settledFrame = frame;
        _projectedEquationCommands = default;
        ClearGeometryCaches();
    }

    public StreamGeometry Geometry(GraphPath path)
    {
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
        if (_lastDashedPath is { } last && ReferenceEquals(last.Path, path) && last.Width == paint.StrokeWidth)
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
        for (int index = 0; index < _penCount; index++)
        {
            ref readonly var entry = ref _pens[index];
            if (PaintEquals(entry.Key, paint))
            {
                return entry.Value!;
            }
        }

        if (_penCount >= MaximumPens)
        {
            ClearPens();
        }

        ImmutableDashStyle? dash = paint.LineStyle switch
        {
            LineStyle.Dot => DotDashStyle,
            LineStyle.Dash => DashDashStyle,
            LineStyle.DashDot => DashDotDashStyle,
            LineStyle.DashDotDot => DashDotDotDashStyle,
            _ => null
        };
        var pen = new ImmutablePen(Brush(paint.Color), paint.StrokeWidth, dash, paint.LineStyle == LineStyle.Dot ? PenLineCap.Round : PenLineCap.Flat, PenLineJoin.Round, 10);
        _pens[_penCount++] = (paint, pen);
        return pen;
    }

    public StreamGeometry HatchGeometry(HatchGridCommand hatch)
    {
        if (_hatchGeometries.TryGetValue(hatch, out StreamGeometry? geometry))
        {
            return geometry;
        }

        geometry = AvaloniaGraphDrawingTarget.CreateHatchGeometry(hatch, out int pointCount);
        if (_hatchGeometries.Count < MaximumHatchGeometries && pointCount <= MaximumGeometryPoints - _cachedGeometryPoints - _cachedHatchGeometryPoints)
        {
            _hatchGeometries.Add(hatch, geometry);
            _cachedHatchGeometryPoints += pointCount;
        }

        return geometry;
    }

    public ImmutableSolidColorBrush Brush(Graphing.Color color)
    {
        for (int index = 0; index < _brushCount; index++)
        {
            ref readonly var entry = ref _brushes[index];
            if (entry.Key.PackedValue == color.PackedValue)
            {
                return entry.Value!;
            }
        }

        if (_brushCount >= MaximumBrushes)
        {
            ClearBrushes();
        }

        var brush = new ImmutableSolidColorBrush(AvaloniaColor.FromArgb(color.A, color.R, color.G, color.B));
        _brushes[_brushCount++] = (color, brush);
        return brush;
    }

    public TextLayout Format(GlyphCommand glyph)
    {
        var key = new AvaloniaGraphRenderCacheTextLayoutKey(glyph.Text, glyph.FontFamily, glyph.FontSize, glyph.FontStyle, glyph.Paint.Color);
        for (int index = 0; index < _textLayoutCount; index++)
        {
            ref readonly var entry = ref _textLayouts[index];
            if (TextLayoutKeyEquals(entry.Key, key))
            {
                return entry.Value!;
            }
        }

        if (_textLayoutCount >= MaximumTextLayouts)
        {
            ClearTextLayouts();
        }

        Typeface typeface = glyph.FontStyle == GraphFontStyle.Italic
            ? ItalicGraphTypeface
            : GraphTypeface;
        var layout = new TextLayout(glyph.Text, typeface, glyph.FontSize, Brush(glyph.Paint.Color));
        _textLayouts[_textLayoutCount++] = (key, layout);
        return layout;
    }

    public void Clear()
    {
        _settledFrame = null;
        _projectedEquationCommands = default;
        ClearBrushes();
        ClearPens();
        ClearGeometryCaches();
        ClearTextLayouts();
    }

    private void ClearGeometryCaches()
    {
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

    private static ImmutableArray<GraphFrameCommand> FindEquationCommands(GraphFrame frame)
    {
        foreach (GraphFrameCommand command in frame.Commands)
        {
            if (command is CommandGroupCommand group)
            {
                return group.Commands;
            }
        }

        return default;
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

    private bool CanCache(GraphPath path) => _geometries.Count + _dashedGeometries.Count + _hatchGeometries.Count < MaximumGeometries && path.Points.Length <= MaximumGeometryPoints - _cachedGeometryPoints - _cachedHatchGeometryPoints;
    private void ClearBrushes()
    {
        Array.Clear(_brushes, 0, _brushCount);
        _brushCount = 0;
    }

    private void ClearPens()
    {
        Array.Clear(_pens, 0, _penCount);
        _penCount = 0;
    }

    private void ClearTextLayouts()
    {
        for (int index = 0; index < _textLayoutCount; index++)
        {
            _textLayouts[index].Value?.Dispose();
        }

        Array.Clear(_textLayouts, 0, _textLayoutCount);
        _textLayoutCount = 0;
    }

    private static bool PaintEquals(GraphPaint left, GraphPaint right) =>
        left.Color.PackedValue == right.Color.PackedValue &&
        left.StrokeWidth.Equals(right.StrokeWidth) &&
        left.LineStyle == right.LineStyle &&
        left.AntiAlias == right.AntiAlias;

    private static bool TextLayoutKeyEquals(
        AvaloniaGraphRenderCacheTextLayoutKey left,
        AvaloniaGraphRenderCacheTextLayoutKey right) =>
        string.Equals(left.Text, right.Text, StringComparison.Ordinal) &&
        string.Equals(left.FontFamily, right.FontFamily, StringComparison.Ordinal) &&
        left.FontSize.Equals(right.FontSize) &&
        left.FontStyle == right.FontStyle &&
        left.Color.PackedValue == right.Color.PackedValue;

    private static ImmutableDashStyle ToImmutable(IDashStyle dashStyle) => new(dashStyle.Dashes, dashStyle.Offset);
}
