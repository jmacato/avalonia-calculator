using Graphing;
using JsMath.Port;

namespace GraphingImpl;

internal sealed record ManagedGraphPrepareRequest(
    long Generation,
    GraphSnapshot Snapshot,
    SamplingViewport Viewport,
    double SamplingScale,
    EvalTrigUnitMode TrigMode,
    ulong MaximumMilliseconds);
