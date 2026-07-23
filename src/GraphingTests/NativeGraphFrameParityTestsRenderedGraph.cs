using Graphing;
using Graphing.Renderer;

namespace GraphingTests;

internal sealed record NativeGraphFrameParityTestsRenderedGraph(IGraph Graph, IEquation Equation, IGraphRenderer Renderer, GraphFrame Frame);
