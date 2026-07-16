using System.Collections.Immutable;
using Graphing;

namespace JsMath.Port;

public enum SampleState
{
    Finite = 0,
    Undefined = 1,
    NonReal = 2,
    Overflow = 3,
    BudgetExceeded = 4
}

public readonly record struct CurveSample(
    double Parameter,
    double X,
    double Y,
    SampleState State)
{
    public bool IsFinite =>
        State == SampleState.Finite && double.IsFinite(X) && double.IsFinite(Y);

    public static CurveSample Undefined(double parameter, SampleState state = SampleState.Undefined) =>
        new(parameter, double.NaN, double.NaN, state);
}

public delegate CurveSample CurveEvaluator(double parameter);

public delegate double ImplicitEvaluator(double x, double y);

public readonly record struct SamplingViewport(
    AxisRange XRange,
    AxisRange YRange,
    double Width,
    double Height)
{
    public bool IsValid =>
        XRange.IsFiniteAndOrdered &&
        YRange.IsFiniteAndOrdered &&
        double.IsFinite(Width) &&
        double.IsFinite(Height) &&
        Width > 0 &&
        Height > 0;

    public GraphPoint ToScreen(double x, double y) => new(
        ((x - XRange.Minimum) / XRange.Length) * Width,
        ((YRange.Maximum - y) / YRange.Length) * Height);

    public GraphPoint ToUser(double screenX, double screenY) => new(
        XRange.Minimum + ((screenX / Width) * XRange.Length),
        YRange.Maximum - ((screenY / Height) * YRange.Length));

    public bool ContainsWithMargin(GraphPoint point, double margin) =>
        point.X >= -margin && point.X <= Width + margin &&
        point.Y >= -margin && point.Y <= Height + margin;
}

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

/// <summary>
/// Settled components retain only finite coordinates. Sampling parameters and
/// failure states are transient and would otherwise double the live point data.
/// </summary>
public sealed record SampledComponent(ImmutableArray<GraphPoint> Points);

public sealed record SampledCurve(
    ImmutableArray<SampledComponent> Components,
    int EvaluationCount,
    int VertexCount,
    bool HasMissingData,
    bool BudgetExceeded);

public readonly record struct ImplicitTraceOptions(
    int SeedColumns = 48,
    int SeedRows = 48,
    int MaximumVertices = 65_536,
    int MaximumNewtonSteps = 8,
    double NewtonTolerance = 1e-7,
    double StepInPixels = 2.5,
    double LoopDistanceFactor = 0.09,
    double LoopDirectionDot = 0.99);
