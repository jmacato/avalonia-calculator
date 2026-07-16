using Graphing;
using Graphing.Renderer;

namespace GraphingTests;

public sealed class GraphInteractionMemoryTests
{
    [Fact]
    public void RapidRangeChangesAreCoalescedUntilAFrameIsRequested()
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.ParsingOptions().SetFormatType(FormatType.Linear);
        IExpression expression = solver.ParseInput("sin(100*x)", out int errorCode, out int errorType)
            ?? throw new InvalidOperationException($"Parse failed: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));

        IGraphRenderer renderer = graph.GetRenderer();
        Assert.Equal(GraphStatus.Ok, renderer.SetGraphSize(1_200, 727));
        Assert.Equal(GraphStatus.Ok, renderer.PrepareGraph());
        GraphFrame settled = Assert.IsType<GraphFrame>(renderer.CurrentFrame);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        GraphStatus lastStatus = GraphStatus.Ok;
        for (int index = 0; index < 10_001; index++)
        {
            double amount = index % 2 == 0 ? 0.001 : -0.001;
            lastStatus = renderer.MoveRangeByRatio(amount, 0);
            if (lastStatus != GraphStatus.Ok)
            {
                break;
            }
        }

        long interactionBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.Equal(GraphStatus.Ok, lastStatus);
        Assert.True(
            interactionBytes < 64 * 1_024,
            $"Range changes allocated {interactionBytes:N0} bytes before a frame was requested.");

        GraphFrame preview = Assert.IsType<GraphFrame>(renderer.CurrentFrame);
        Assert.NotSame(settled, preview);
        Assert.True(preview.IsStale);
        Assert.Equal(GraphStatus.Ok, renderer.PrepareGraph());
        Assert.False(Assert.IsType<GraphFrame>(renderer.CurrentFrame).IsStale);
    }
}
