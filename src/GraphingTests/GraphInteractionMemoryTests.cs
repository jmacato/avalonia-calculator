using System.Diagnostics;
using System.Collections.Immutable;
using Avalonia.Headless.XUnit;
using Graphing;
using Graphing.Renderer;

namespace GraphingTests;

public sealed class GraphInteractionMemoryTests
{
    [AvaloniaFact]
    public void ProjectedFrameCacheRetainsOnlyTheCurrentEquationGeometry()
    {
        var cache = new GraphControl.AvaloniaGraphRenderCache();
        var firstPath = new GraphPath(Enumerable.Range(0, 8).Select(index => new GraphPoint(index, index)));
        ImmutableArray<GraphFrameCommand> firstCommands = [new StrokePathCommand(firstPath, new GraphPaint(new Color(0, 0, 0)))];
        var firstFrame = new GraphFrame(393, 659, 96, 96, 1, new Color(255, 255, 255), [new CommandGroupCommand(firstCommands)]);

        cache.BeginFrame(firstFrame);
        object firstGeometry = cache.Geometry(firstPath);
        var sameSourceFrame = new GraphFrame(393, 659, 96, 96, 1, new Color(255, 255, 255), [new CommandGroupCommand(firstCommands)]);
        cache.BeginFrame(sameSourceFrame);
        Assert.Same(firstGeometry, cache.Geometry(firstPath));

        var replacementPath = new GraphPath(Enumerable.Range(0, 8).Select(index => new GraphPoint(index, 8 - index)));
        ImmutableArray<GraphFrameCommand> replacementCommands = [new StrokePathCommand(replacementPath, new GraphPaint(new Color(0, 0, 0)))];
        var replacementFrame = new GraphFrame(393, 659, 96, 96, 1, new Color(255, 255, 255), [new CommandGroupCommand(replacementCommands)]);
        cache.BeginFrame(replacementFrame);

        Assert.NotSame(firstGeometry, cache.Geometry(firstPath));
    }

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

        GraphFrame current = Assert.IsType<GraphFrame>(renderer.CurrentFrame);
        Assert.NotSame(settled, current);
        Assert.Contains(current.Commands, command => command is PushCoordinateTransformCommand);
        Assert.Equal(GraphStatus.Ok, renderer.PrepareGraph());
        Assert.DoesNotContain(Assert.IsType<GraphFrame>(renderer.CurrentFrame).Commands, command => command is PushCoordinateTransformCommand);
    }

    [Fact]
    public void InteractionFramesRefreshViewportFurnitureAndReuseSampledPaths()
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.ParsingOptions().SetFormatType(FormatType.Linear);
        IExpression expression = solver.ParseInput("sin(100*x)", out _, out _)!;
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));

        IGraphRenderer renderer = graph.GetRenderer();
        Assert.Equal(GraphStatus.Ok, renderer.SetGraphSize(1_200, 727));
        Assert.Equal(GraphStatus.Ok, renderer.PrepareGraph());
        GraphFrame settled = Assert.IsType<GraphFrame>(renderer.CurrentFrame);
        GraphPath sampledPath = settled.Commands
            .OfType<StrokePathCommand>()
            .Select(command => command.Path)
            .MaxBy(path => path.Points.Length)!;
        Assert.Contains(sampledPath.Points, point => point.X < 0);
        Assert.Contains(sampledPath.Points, point => point.X > settled.Width);

        Assert.Equal(GraphStatus.Ok, renderer.MoveRangeByRatio(0.01, -0.01));
        GraphFrame current = Assert.IsType<GraphFrame>(renderer.CurrentFrame);

        Assert.Contains(current.Commands, command => command is PushCoordinateTransformCommand);
        Assert.IsType<PushClipCommand>(current.Commands[0]);
        CommandGroupCommand equations = Assert.Single(current.Commands.OfType<CommandGroupCommand>());
        Assert.Contains(
            equations.Commands.OfType<StrokePathCommand>(),
            command => ReferenceEquals(sampledPath, command.Path));
        Assert.Contains(current.Commands, command => command is GlyphCommand);
        Assert.DoesNotContain(equations.Commands, command => command is GlyphCommand);
    }

    [Fact]
    public void InteractionFrameAllocationRemainsBounded()
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.ParsingOptions().SetFormatType(FormatType.Linear);
        IExpression expression = solver.ParseInput("sin(100*x)", out _, out _)!;
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));

        IGraphRenderer renderer = graph.GetRenderer();
        Assert.Equal(GraphStatus.Ok, renderer.SetGraphSize(1_200, 727));
        Assert.Equal(GraphStatus.Ok, renderer.PrepareGraph());
        for (int index = 0; index < 20; index++)
        {
            Assert.Equal(GraphStatus.Ok, renderer.MoveRangeByRatio(0.0001, 0));
            Assert.NotNull(renderer.CurrentFrame);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        const int frameCount = 1_000;
        GraphStatus lastStatus = GraphStatus.Ok;
        GraphFrame? lastFrame = null;
        for (int index = 0; index < frameCount; index++)
        {
            lastStatus = renderer.MoveRangeByRatio(0.0001, 0);
            lastFrame = renderer.CurrentFrame;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(GraphStatus.Ok, lastStatus);
        Assert.NotNull(lastFrame);
        Assert.True(
            allocated < frameCount * 12 * 1_024,
            $"Interaction frames allocated {allocated:N0} bytes ({allocated / (double)frameCount:N1}/frame).");
    }

    [Fact]
    public void DomainLimitedOscillationRetainsOnlyTheCurrentSampleSetDuringInteraction()
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.ParsingOptions().SetFormatType(FormatType.Linear);
        IExpression expression = solver.ParseInput("sin(x^3+sqrt(x))", out int errorCode, out int errorType)
            ?? throw new InvalidOperationException($"Parse failed: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));

        IGraphRenderer renderer = graph.GetRenderer();
        Assert.Equal(GraphStatus.Ok, renderer.SetGraphSize(393, 659));
        Assert.Equal(GraphStatus.Ok, renderer.PrepareGraph());

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long retainedBefore = GC.GetTotalMemory(forceFullCollection: true);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        const int refreshCount = 24;
        int maximumPathVertices = 0;
        for (int index = 0; index < refreshCount; index++)
        {
            double scale = 1 - ((index % 7) * 0.035);
            double xMinimum = -10 + (index * 0.31);
            double xMaximum = xMinimum + (20 * scale);
            double yHalfRange = 10 * scale;
            Assert.Equal(GraphStatus.Ok, renderer.SetDisplayRanges(xMinimum, xMaximum, -yHalfRange, yHalfRange));
            Assert.NotNull(renderer.CurrentFrame);
            Assert.Equal(GraphStatus.Ok, renderer.PrepareGraph());
            GraphFrame frame = Assert.IsType<GraphFrame>(renderer.CurrentFrame);
            maximumPathVertices = Math.Max(maximumPathVertices, MaximumStrokePathVertices(frame.Commands));
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long retained = GC.GetTotalMemory(forceFullCollection: true) - retainedBefore;

        Assert.InRange(maximumPathVertices, 2, 4 * (393 + 659));
        Assert.True(
            allocated < 6 * 1_024 * 1_024,
            $"Repeated complex refreshes retained {retained:N0} bytes; allocated {allocated:N0} bytes and reached {maximumPathVertices:N0} vertices.");
        Assert.True(
            retained < 4 * 1_024 * 1_024,
            $"Repeated complex refreshes retained {retained:N0} bytes; allocated {allocated:N0} bytes and reached {maximumPathVertices:N0} vertices.");
    }

    [Fact]
    public void ConcurrentInteractionRefreshCommitsOnlyTheLatestViewport()
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.ParsingOptions().SetFormatType(FormatType.Linear);
        IExpression expression = solver.ParseInput("sin(x^3+sqrt(x))", out int errorCode, out int errorType)
            ?? throw new InvalidOperationException($"Parse failed: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));

        IGraphRenderer renderer = graph.GetRenderer();
        IConcurrentGraphRenderer concurrentRenderer = Assert.IsAssignableFrom<IConcurrentGraphRenderer>(renderer);
        Assert.Equal(GraphStatus.Ok, renderer.SetGraphSize(393, 659));
        Assert.Equal(GraphStatus.Ok, renderer.PrepareGraph());

        const int refreshCount = 48;
        for (int index = 0; index < refreshCount; index++)
        {
            double xMinimum = -9.8 + (index * 0.2);
            Assert.Equal(GraphStatus.Ok, renderer.SetDisplayRanges(xMinimum, xMinimum + 20, -10, 10));
            Assert.Equal(GraphStatus.Ok, concurrentRenderer.RequestPrepareGraph());
            Assert.Contains(Assert.IsType<GraphFrame>(renderer.CurrentFrame).Commands, command => command is PushCoordinateTransformCommand);
        }

        var timeout = Stopwatch.StartNew();
        bool completed = false;
        GraphStatus completionStatus = GraphStatus.Ok;
        while (!completed && timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            completionStatus = concurrentRenderer.TryCommitPreparedGraph(out completed);
            Thread.Yield();
        }

        Assert.True(completed, "The latest bounded-channel graph request did not complete.");
        Assert.Equal(GraphStatus.Ok, completionStatus);
        Assert.False(concurrentRenderer.IsPrepareGraphPending);
        Assert.DoesNotContain(Assert.IsType<GraphFrame>(renderer.CurrentFrame).Commands, command => command is PushCoordinateTransformCommand);
        Assert.Equal(GraphStatus.Ok, renderer.GetDisplayRanges(out double finalXMinimum, out double finalXMaximum, out _, out _));
        Assert.Equal(-0.4, finalXMinimum, precision: 10);
        Assert.Equal(19.6, finalXMaximum, precision: 10);
    }

    [Fact]
    public void ConcurrentInteractionCommitReusesCompletedGeometryAtTheCurrentViewport()
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.ParsingOptions().SetFormatType(FormatType.Linear);
        IExpression expression = solver.ParseInput("sin(x^3+sqrt(x))", out int errorCode, out int errorType)
            ?? throw new InvalidOperationException($"Parse failed: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));

        IGraphRenderer renderer = graph.GetRenderer();
        IConcurrentGraphRenderer concurrentRenderer = Assert.IsAssignableFrom<IConcurrentGraphRenderer>(renderer);
        Assert.Equal(GraphStatus.Ok, renderer.SetGraphSize(393, 659));
        Assert.Equal(GraphStatus.Ok, renderer.PrepareGraph());

        Assert.Equal(GraphStatus.Ok, renderer.SetDisplayRanges(-8, 12, -10, 10));
        Assert.Equal(GraphStatus.Ok, concurrentRenderer.RequestPrepareGraph());
        Assert.Equal(GraphStatus.Ok, renderer.SetDisplayRanges(-5, 15, -8, 12));

        var timeout = Stopwatch.StartNew();
        bool completed = false;
        GraphStatus completionStatus = GraphStatus.Ok;
        while (!completed && timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            completionStatus = concurrentRenderer.TryCommitPreparedGraph(out completed);
            Thread.Yield();
        }

        Assert.True(completed, "The reusable intermediate graph request did not complete.");
        Assert.Equal(GraphStatus.Ok, completionStatus);
        GraphFrame frame = Assert.IsType<GraphFrame>(renderer.CurrentFrame);
        Assert.Contains(frame.Commands, command => command is PushCoordinateTransformCommand);
        Assert.True(concurrentRenderer.TryGetPreparedDisplayRanges(out double preparedXMinimum, out double preparedXMaximum, out double preparedYMinimum, out double preparedYMaximum));
        Assert.Equal(-8, preparedXMinimum, precision: 10);
        Assert.Equal(12, preparedXMaximum, precision: 10);
        Assert.Equal(-10, preparedYMinimum, precision: 10);
        Assert.Equal(10, preparedYMaximum, precision: 10);
    }

    [Fact]
    public void DisposingGraphCancelsAndDrainsConcurrentPreparation()
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.ParsingOptions().SetFormatType(FormatType.Linear);
        IExpression expression = solver.ParseInput("sin(x^3+sqrt(x))", out _, out _)!;
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));

        IGraphRenderer renderer = graph.GetRenderer();
        IConcurrentGraphRenderer concurrentRenderer = Assert.IsAssignableFrom<IConcurrentGraphRenderer>(renderer);
        Assert.Equal(GraphStatus.Ok, renderer.SetGraphSize(1_200, 727));
        Assert.Equal(GraphStatus.Ok, concurrentRenderer.RequestPrepareGraph());

        IDisposable disposableGraph = Assert.IsAssignableFrom<IDisposable>(graph);
        disposableGraph.Dispose();

        Assert.False(concurrentRenderer.IsPrepareGraphPending);
        Assert.Equal(GraphStatus.Cancelled, concurrentRenderer.RequestPrepareGraph());
        Assert.Equal(GraphStatus.Cancelled, concurrentRenderer.TryCommitPreparedGraph(out bool completed));
        Assert.False(completed);
    }

    [Fact]
    public void ReleasingPreparedResourcesKeepsRendererReusable()
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.ParsingOptions().SetFormatType(FormatType.Linear);
        IExpression expression = solver.ParseInput("sin(x^3+sqrt(x))", out _, out _)!;
        IGraph graph = solver.CreateGrapher();
        Assert.NotNull(graph.TryInitialize(expression));

        IGraphRenderer renderer = graph.GetRenderer();
        IConcurrentGraphRenderer concurrentRenderer = Assert.IsAssignableFrom<IConcurrentGraphRenderer>(renderer);
        Assert.Equal(GraphStatus.Ok, renderer.SetGraphSize(1_200, 727));
        Assert.Equal(GraphStatus.Ok, renderer.PrepareGraph());
        Assert.True(concurrentRenderer.TryGetPreparedDisplayRanges(out _, out _, out _, out _));

        concurrentRenderer.ReleasePreparedResources();

        Assert.False(concurrentRenderer.IsPrepareGraphPending);
        Assert.False(concurrentRenderer.TryGetPreparedDisplayRanges(out _, out _, out _, out _));
        Assert.Equal(GraphStatus.Ok, concurrentRenderer.RequestPrepareGraph());

        var timeout = Stopwatch.StartNew();
        bool completed = false;
        while (!completed && timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            Assert.Equal(GraphStatus.Ok, concurrentRenderer.TryCommitPreparedGraph(out completed));
            Thread.Yield();
        }

        Assert.True(completed, "The suspended renderer did not prepare after reuse.");
        Assert.True(concurrentRenderer.TryGetPreparedDisplayRanges(out _, out _, out _, out _));
        Assert.NotNull(renderer.CurrentFrame);
        Assert.Same(renderer, graph.GetRenderer());
        Assert.IsAssignableFrom<IDisposable>(graph).Dispose();
    }

    private static int MaximumStrokePathVertices(IEnumerable<GraphFrameCommand> commands)
    {
        int maximum = 0;
        foreach (GraphFrameCommand command in commands)
        {
            if (command is StrokePathCommand stroke)
            {
                maximum = Math.Max(maximum, stroke.Path.Points.Length);
            }
            else if (command is CommandGroupCommand group)
            {
                maximum = Math.Max(maximum, MaximumStrokePathVertices(group.Commands));
            }
        }

        return maximum;
    }
}
