using System.Collections.Immutable;
using Graphing;

namespace JsMath.Port;
/// <summary>
/// Settled components retain only finite coordinates. Sampling parameters and
/// failure states are transient and would otherwise double the live point data.
/// </summary>
public sealed record SampledComponent(ImmutableArray<GraphPoint> Points);
