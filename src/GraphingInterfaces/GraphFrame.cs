using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;
/// <summary>
/// Immutable, device-independent drawing commands. Coordinates are logical DIPs
/// with a top-left origin; Cartesian conversion happens before frame creation.
/// </summary>
public sealed class GraphFrame
{
    public GraphFrame(uint width, uint height, float dpiX, float dpiY, long revision, Color background, IEnumerable<GraphFrameCommand> commands, bool hasSomeMissingData = false, bool isStale = false) : this(width, height, dpiX, dpiY, revision, background, commands.ToImmutableArray(), hasSomeMissingData, isStale)
    {
    }

    public GraphFrame(uint width, uint height, float dpiX, float dpiY, long revision, Color background, ImmutableArray<GraphFrameCommand> commands, bool hasSomeMissingData = false, bool isStale = false)
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
