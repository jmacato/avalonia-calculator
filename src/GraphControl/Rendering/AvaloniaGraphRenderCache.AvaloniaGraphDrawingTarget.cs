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
    private readonly Dictionary<GraphPath, StreamGeometry> _geometries = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(GraphPath Path, float Width), StreamGeometry> _dashedGeometries = [];
    private readonly Dictionary<HatchGridCommand, StreamGeometry> _hatchGeometries = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Graphing.Color, ImmutableSolidColorBrush> _brushes = [];
    private readonly Dictionary<GraphPaint, ImmutablePen> _pens = [];
    private readonly Dictionary<AvaloniaGraphRenderCacheTextLayoutKey, TextLayout> _textLayouts = [];
    private GraphFrame? _settledFrame;
    private ImmutableArray<GraphFrameCommand> _projectedEquationCommands;
    private GraphPath? _lastPath;
    private StreamGeometry? _lastGeometry;
    private (GraphPath Path, float Width)? _lastDashedPath;
    private StreamGeometry? _lastDashedGeometry;
    private int _cachedGeometryPoints;
    private int _cachedHatchGeometryPoints;
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
        pen = new ImmutablePen(Brush(paint.Color), paint.StrokeWidth, dash, paint.LineStyle == LineStyle.Dot ? PenLineCap.Round : PenLineCap.Flat, PenLineJoin.Round, 10);
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
        if (_hatchGeometries.Count < MaximumHatchGeometries && pointCount <= MaximumGeometryPoints - _cachedGeometryPoints - _cachedHatchGeometryPoints)
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
        var key = new AvaloniaGraphRenderCacheTextLayoutKey(glyph.Text, glyph.FontFamily, glyph.FontSize, glyph.FontStyle, glyph.Paint.Color);
        if (_textLayouts.TryGetValue(key, out TextLayout? layout))
        {
            return layout;
        }

        if (_textLayouts.Count >= MaximumTextLayouts)
        {
            ClearTextLayouts();
        }

        layout = new TextLayout(glyph.Text, new Typeface(glyph.FontFamily, glyph.FontStyle == GraphFontStyle.Italic ? FontStyle.Italic : FontStyle.Normal), glyph.FontSize, Brush(glyph.Paint.Color));
        _textLayouts.Add(key, layout);
        return layout;
    }

    public void Clear()
    {
        _settledFrame = null;
        _projectedEquationCommands = default;
        _brushes.Clear();
        _pens.Clear();
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
    private void ClearTextLayouts()
    {
        foreach (TextLayout layout in _textLayouts.Values)
        {
            layout.Dispose();
        }

        _textLayouts.Clear();
    }

    private static ImmutableDashStyle ToImmutable(IDashStyle dashStyle) => new(dashStyle.Dashes, dashStyle.Offset);
}
