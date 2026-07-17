using System.Buffers;
using System.Threading.Channels;
using Graphing;
using Graphing.Analyzer;

namespace GraphControl;

internal sealed class FunctionAnalysisWorker : IDisposable
{
    private static readonly SearchValues<char> UnsupportedRelations =
        SearchValues.Create("<>≤≥≠");

    private readonly Channel<FunctionAnalysisWorkItem> _requests =
        Channel.CreateBounded<FunctionAnalysisWorkItem>(
            new BoundedChannelOptions(1)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
    private readonly Task _worker;
    private int _disposed;

    public FunctionAnalysisWorker()
    {
        // Task.Run is intentional here. Calling the async loop directly can
        // execute expensive work on the caller when a channel read completes
        // synchronously, which is exactly the UI-thread stall this worker owns.
        _worker = Task.Run(ProcessRequestsAsync);
    }

    public async Task<KeyGraphFeaturesInfo> AnalyzeAsync(
        FunctionAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref _disposed) != 0)
        {
            return new KeyGraphFeaturesInfo(AnalysisErrorType.AnalysisCouldNotBePerformed);
        }

        Channel<FunctionAnalysisOutcome> response =
            Channel.CreateBounded<FunctionAnalysisOutcome>(
                new BoundedChannelOptions(1)
                {
                    AllowSynchronousContinuations = false,
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = true
                });
        try
        {
            await _requests.Writer.WriteAsync(
                new FunctionAnalysisWorkItem(
                    request,
                    response.Writer,
                    cancellationToken),
                cancellationToken).ConfigureAwait(false);
            FunctionAnalysisOutcome outcome = await response.Reader
                .ReadAsync(cancellationToken)
                .ConfigureAwait(false);
            if (outcome.IsCancelled)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            return outcome.Result ??
                new KeyGraphFeaturesInfo(AnalysisErrorType.AnalysisCouldNotBePerformed);
        }
        catch (ChannelClosedException) when (Volatile.Read(ref _disposed) != 0)
        {
            return new KeyGraphFeaturesInfo(AnalysisErrorType.AnalysisCouldNotBePerformed);
        }
    }

    public static KeyGraphFeaturesInfo AnalyzeCore(
        FunctionAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Expression.AsSpan().IndexOfAny(UnsupportedRelations) >= 0)
        {
            return new KeyGraphFeaturesInfo(AnalysisErrorType.AnalysisNotSupported);
        }

        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.ParsingOptions().SetFormatType(FormatType.Linear);
        solver.FormatOptions().SetFormatType(FormatType.MathML);
        solver.FormatOptions().SetMathMLPrefix("mml");
        solver.EvalOptions().SetTrigUnitMode(request.TrigUnitMode);
        solver.ParsingOptions().SetLocalizationType(request.Localization);
        solver.FormatOptions().SetLocalizationType(request.Localization);
        IExpression? expression = solver.ParseInput(request.Expression, out _, out _);
        if (expression is null)
        {
            return new KeyGraphFeaturesInfo(AnalysisErrorType.AnalysisCouldNotBePerformed);
        }

        cancellationToken.ThrowIfCancellationRequested();
        IGraph analysisGraph = solver.CreateGrapher();
        try
        {
            if (analysisGraph.TryInitialize(expression) is not { Count: > 0 })
            {
                return new KeyGraphFeaturesInfo(AnalysisErrorType.AnalysisCouldNotBePerformed);
            }

            foreach ((string variableName, double value) in request.Variables)
            {
                cancellationToken.ThrowIfCancellationRequested();
                analysisGraph.SetArgValue(variableName, value);
            }

            IGraphAnalyzer analyzer = analysisGraph.GetAnalyzer();
            if (!analyzer.CanFunctionAnalysisBePerformed(out bool variableIsNotX))
            {
                return new KeyGraphFeaturesInfo(variableIsNotX
                    ? AnalysisErrorType.VariableIsNotX
                    : AnalysisErrorType.AnalysisNotSupported);
            }

            if (variableIsNotX)
            {
                return new KeyGraphFeaturesInfo(AnalysisErrorType.VariableIsNotX);
            }

            GraphStatus status = analyzer is ICancellableGraphAnalyzer cancellable
                ? cancellable.PerformFunctionAnalysis(
                    (uint)PerformAnalysisType.All,
                    cancellationToken)
                : analyzer.PerformFunctionAnalysis((uint)PerformAnalysisType.All);
            if (status == GraphStatus.Cancelled && cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (status != GraphStatus.Ok)
            {
                return new KeyGraphFeaturesInfo(AnalysisErrorType.AnalysisCouldNotBePerformed);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new KeyGraphFeaturesInfo(solver.Analyze(analyzer));
        }
        finally
        {
            (analysisGraph as IDisposable)?.Dispose();
        }
    }

    private async Task ProcessRequestsAsync()
    {
        await foreach (FunctionAnalysisWorkItem work in _requests.Reader.ReadAllAsync()
                           .ConfigureAwait(false))
        {
            FunctionAnalysisOutcome outcome;
            if (work.CancellationToken.IsCancellationRequested ||
                Volatile.Read(ref _disposed) != 0)
            {
                outcome = FunctionAnalysisOutcome.Cancelled;
            }
            else
            {
                try
                {
                    outcome = FunctionAnalysisOutcome.Completed(
                        AnalyzeCore(work.Request, work.CancellationToken));
                }
                catch (OperationCanceledException)
                {
                    outcome = FunctionAnalysisOutcome.Cancelled;
                }
                catch (Exception exception) when (
                    exception is ArgumentException or
                    InvalidOperationException or
                    NotSupportedException)
                {
                    outcome = FunctionAnalysisOutcome.Completed(
                        new KeyGraphFeaturesInfo(
                            AnalysisErrorType.AnalysisCouldNotBePerformed));
                }
            }

            work.Response.TryWrite(outcome);
            work.Response.TryComplete();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _requests.Writer.TryComplete();
        GC.SuppressFinalize(this);
    }
}
