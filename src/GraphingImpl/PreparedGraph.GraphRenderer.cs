using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using Graphing;
using Graphing.Renderer;
using GraphingRaster.Skia;
using JsMath.Port;

namespace GraphingImpl;

internal sealed record PreparedGraph(GraphSnapshot Snapshot, SamplingViewport Viewport, ImmutableArray<PreparedEquationGeometry> Equations, bool HasMissingData);
