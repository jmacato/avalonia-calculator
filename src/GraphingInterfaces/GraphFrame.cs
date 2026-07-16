using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;

public enum GraphCommandKind
{
    PushClip = 0,
    PopClip = 1,
    StrokePath = 2,
    FillPath = 3,
    Marker = 4,
    Glyph = 5,
    GlyphBackground = 6,
    PushCoordinateTransform = 7,
    PopCoordinateTransform = 8,
    CommandGroup = 9,
    HatchGrid = 10
}

public enum GraphMarkerShape
{
    Circle = 0,
    Square = 1,
    Diamond = 2,
    Cross = 3
}

public enum GraphTextAlignment
{
    Start = 0,
    Center = 1,
    End = 2
}

public enum GraphFontStyle
{
    Normal = 0,
    Italic = 1
}

public readonly record struct GraphPaint(
    Color Color,
    float StrokeWidth = 1f,
    LineStyle LineStyle = LineStyle.Solid,
    bool AntiAlias = true);

/// <summary>
/// Transforms command coordinates while leaving device-sized paint properties
/// such as stroke widths, marker radii and font sizes unchanged.
/// </summary>
public readonly record struct GraphCoordinateTransform(
    double ScaleX,
    double ScaleY,
    double OffsetX,
    double OffsetY)
{
    public GraphPoint Transform(GraphPoint point) => new(
        (point.X * ScaleX) + OffsetX,
        (point.Y * ScaleY) + OffsetY);
}

public sealed record GraphPath
{
    public GraphPath(IEnumerable<GraphPoint> points, bool isClosed = false)
        : this(points.ToImmutableArray(), isClosed)
    {
    }

    public GraphPath(ImmutableArray<GraphPoint> points, bool isClosed = false)
    {
        Points = points.IsDefault ? ImmutableArray<GraphPoint>.Empty : points;
        IsClosed = isClosed;
    }

    public ImmutableArray<GraphPoint> Points { get; }

    public bool IsClosed { get; }
}

public abstract record GraphFrameCommand(GraphCommandKind Kind);

public sealed record PushClipCommand(GraphRect Clip)
    : GraphFrameCommand(GraphCommandKind.PushClip);

public sealed record PopClipCommand()
    : GraphFrameCommand(GraphCommandKind.PopClip);

public sealed record PushCoordinateTransformCommand(GraphCoordinateTransform Transform)
    : GraphFrameCommand(GraphCommandKind.PushCoordinateTransform);

public sealed record PopCoordinateTransformCommand()
    : GraphFrameCommand(GraphCommandKind.PopCoordinateTransform);

public sealed record CommandGroupCommand(ImmutableArray<GraphFrameCommand> Commands)
    : GraphFrameCommand(GraphCommandKind.CommandGroup);

public sealed record StrokePathCommand(GraphPath Path, GraphPaint Paint)
    : GraphFrameCommand(GraphCommandKind.StrokePath);

public sealed record FillPathCommand(GraphPath Path, GraphPaint Paint)
    : GraphFrameCommand(GraphCommandKind.FillPath);

public sealed record MarkerCommand(
    GraphPoint Center,
    float Radius,
    GraphMarkerShape Shape,
    GraphPaint Fill,
    GraphPaint? Stroke = null)
    : GraphFrameCommand(GraphCommandKind.Marker);

/// <summary>
/// A compact occupancy grid for Calculator's inequality cross hatch. One bit
/// replaces one retained marker command while preserving the native lattice.
/// </summary>
public sealed record HatchGridCommand(
    ImmutableArray<ulong> Occupancy,
    int LatticeIntervals,
    double Width,
    double Height,
    float Radius,
    GraphPaint Paint)
    : GraphFrameCommand(GraphCommandKind.HatchGrid)
{
    public bool IsOccupied(int column, int row)
    {
        int rowsPerColumn = LatticeIntervals - 1;
        if ((uint)column >= (uint)LatticeIntervals || row <= 0 || row >= LatticeIntervals)
        {
            return false;
        }

        int bitIndex = (column * rowsPerColumn) + row - 1;
        int wordIndex = bitIndex >> 6;
        return (uint)wordIndex < (uint)Occupancy.Length &&
               (Occupancy[wordIndex] & (1UL << (bitIndex & 63))) != 0;
    }
}

public sealed record GlyphCommand(
    string Text,
    GraphPoint Origin,
    string FontFamily,
    float FontSize,
    GraphPaint Paint,
    GraphTextAlignment Alignment = GraphTextAlignment.Start,
    GraphFontStyle FontStyle = GraphFontStyle.Normal)
    : GraphFrameCommand(GraphCommandKind.Glyph);

/// <summary>
/// Clears the measured layout rectangle for a positioned glyph without drawing
/// the glyph itself. Keeping this separate lets equation geometry pass over the
/// cleared axes/grid while the glyph is replayed last, matching Calculator's
/// authored draw order.
/// </summary>
public sealed record GlyphBackgroundCommand(GlyphCommand Glyph, GraphPaint Paint)
    : GraphFrameCommand(GraphCommandKind.GlyphBackground);

/// <summary>
/// Immutable, device-independent drawing commands. Coordinates are logical DIPs
/// with a top-left origin; Cartesian conversion happens before frame creation.
/// </summary>
public sealed class GraphFrame
{
    public GraphFrame(
        uint width,
        uint height,
        float dpiX,
        float dpiY,
        long revision,
        Color background,
        IEnumerable<GraphFrameCommand> commands,
        bool hasSomeMissingData = false,
        bool isStale = false)
        : this(
            width,
            height,
            dpiX,
            dpiY,
            revision,
            background,
            commands.ToImmutableArray(),
            hasSomeMissingData,
            isStale)
    {
    }

    public GraphFrame(
        uint width,
        uint height,
        float dpiX,
        float dpiY,
        long revision,
        Color background,
        ImmutableArray<GraphFrameCommand> commands,
        bool hasSomeMissingData = false,
        bool isStale = false)
    {
        if (!float.IsFinite(dpiX) || dpiX <= 0 || !float.IsFinite(dpiY) || dpiY <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpiX), "DPI values must be finite and positive.");
        }

        Width = width;
        Height = height;
        DpiX = dpiX;
        DpiY = dpiY;
        Revision = revision;
        Background = background;
        Commands = commands.IsDefault ? ImmutableArray<GraphFrameCommand>.Empty : commands;
        HasSomeMissingData = hasSomeMissingData;
        IsStale = isStale;
    }

    public uint Width { get; }

    public uint Height { get; }

    public float DpiX { get; }

    public float DpiY { get; }

    public long Revision { get; }

    public Color Background { get; }

    public ImmutableArray<GraphFrameCommand> Commands { get; }

    public bool HasSomeMissingData { get; }

    public bool IsStale { get; }
}

public interface IGraphDrawingTarget
{
    void BeginFrame(GraphFrame frame);

    void Draw(GraphFrameCommand command);

    void EndFrame();
}
