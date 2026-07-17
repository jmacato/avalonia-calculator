using System.Collections.Immutable;
using Graphing;

namespace JsMath.Port;

public sealed record SamplingOptions
{
    public int InitialSegments { get; init; } = 32;
    public int MinimumDepth { get; init; } = 2;
    public int MaximumDepth { get; init; } = 18;
    public int MaximumVertices { get; init; } = 65_536;
    public int MaximumEvaluations { get; init; } = 262_144;
    public double FlatnessTolerance { get; init; } = 0.35;
    public double MaximumSegmentLength { get; init; } = 8;
    public double OffscreenMargin { get; init; } = 500;
    public int BorderProbeIterations { get; init; } = 24;
    public static SamplingOptions Interactive { get; } = new()
    {
        MaximumVertices = 16_384,
        MaximumEvaluations = 65_536,
        MaximumDepth = 16,
        FlatnessTolerance = 0.7,
        MaximumSegmentLength = 12
    };
    public static SamplingOptions Settled { get; } = new();
}
