using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using Graphing;
using Graphing.Renderer;
using GraphingRaster.Skia;
using JsMath.Port;
using SkiaSharp;

namespace GraphingTests;

internal sealed record NativeGraphFrameParityTestsRenderedGraph(IGraph Graph, IEquation Equation, IGraphRenderer Renderer, GraphFrame Frame);
