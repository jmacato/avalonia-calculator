using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace Calculator.BrowserHost;

internal sealed class TelemetryStoreSessionState
{
    private const int MaximumTimelineSamples = 1_200;
    private const int MaximumEvents = 512;
    private const int MaximumInputTraceChunks = 1_200;
    private static readonly TimeSpan WorkerStaleAfter = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan UiStaleAfter = TimeSpan.FromSeconds(4);
    private TelemetryStoreSessionStateState _state;
    private string _lastReportedStatus = string.Empty;
    public TelemetryStoreSessionState(string sessionId, string runId, string remoteAddress, DateTimeOffset now)
    {
        SessionId = sessionId;
        StartedAt = now;
        _state = new TelemetryStoreSessionStateState(runId, remoteAddress, now, null, null, 0, default, default, -1, false, 0, Array.Empty<TelemetryTimelineSample>(), Array.Empty<TelemetryEventView>(), Array.Empty<TelemetryInputTraceChunk>());
    }

    public string SessionId { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset LastReceivedAt => Volatile.Read(ref _state).LastReceivedAt;

    public void Ingest(TelemetryBatch batch, string remoteAddress, DateTimeOffset now)
    {
        TelemetryStoreSessionStateState current = Volatile.Read(ref _state);
        while (true)
        {
            TelemetryUiSnapshot? latestUi = current.LatestUi;
            DateTimeOffset lastDistinctUiAt = current.LastDistinctUiAt;
            long lastUiSequence = current.LastUiSequence;
            TelemetryTimelineSample[] timeline = current.Timeline;
            if (batch.Ui is { } ui)
            {
                latestUi = ui;
                if (ui.Sequence != lastUiSequence)
                {
                    lastUiSequence = ui.Sequence;
                    lastDistinctUiAt = now;
                }

                timeline = AppendCapped(timeline, new TelemetryTimelineSample(now, batch.WorkerSequence, batch.WorkerUptimeMs, batch.WorkerUploadFailures, batch.SkippedUploads, batch.UiAgeMs, ui.Sequence, ui.BootStage ?? string.Empty, ui.Visibility ?? string.Empty, ui.HasFocus, ui.FrameRate, ui.MaxFrameGapMs, ui.MaxFrameGapTotalMs, ui.LongFrameCountTotal, ui.LongTaskCountTotal, ui.MaxLongTaskMs, ui.PointerDownTotal, ui.PointerMoveTotal, ui.PointerMoveSinceLast, ui.PointerUpTotal, ui.PointerCancelTotal, ui.ActivePointers, ui.LastPointerAgeMs, ui.LostPointerCaptureTotal, ui.CanvasPresentSinceLast, ui.CanvasPresentAgeMs, ui.CanvasFrameReceivedTotal, ui.CanvasFramePresentedTotal, ui.CanvasFrameDroppedTotal, ui.CanvasFramePending, ui.InputQueueDepth, ui.InputQueueHighWater, ui.InputQueueDequeued, ui.InputQueueRetries, ui.InputQueueShed, ui.WebGlContextLost, ui.WebGlContextLosses, ui.ActiveElement ?? string.Empty, ui.NativeInputFocused, ui.SelectionChangeTotal, ui.ContextMenuTotal, ui.VisualViewportHeight, ui.InnerHeight, ui.CanvasWidth, ui.CanvasHeight, ui.WasmMemoryBytes, ui.WasmMemoryMaxBytes, ui.JsHeapBytes, ui.ManagedHeapBytes, ui.ManagedAllocatedBytes, ui.ManagedGen0Collections, ui.ManagedGen1Collections, ui.ManagedGen2Collections, ui.ManagedProbeStarted, ui.ManagedProbeInFlight, ui.ManagedSampleAgeMs, ui.ManagedDispatcherPulse, ui.ManagedDispatcherAgeMs, ui.GraphPipelineProbeStarted, ui.GraphPipelineProbeInFlight, ui.GraphRequestedGeneration, ui.GraphWorkerGeneration, ui.GraphCompletedGeneration, ui.GraphPublishedGeneration, ui.GraphCommittedGeneration, ui.GraphWorkerActive, ui.GraphCompletedStatus, ui.GraphCommitStatus, ui.GraphSettlementTimerCount, ui.GraphSettlementRequestCount, ui.GraphRenderCount, ui.GraphRendererActiveCount, ui.GraphRendererCreatedCount, ui.GraphRendererDisposedCount), MaximumTimelineSamples);
            }

            bool pageEnded = current.PageEnded;
            int errorCount = current.ErrorCount;
            TelemetryEventView[] events = AppendEvents(current.Events, batch.Events, now, ref errorCount, ref pageEnded);
            TelemetryInputTraceChunk[] inputTrace = AppendInputTrace(current.InputTrace, batch);
            var replacement = new TelemetryStoreSessionStateState(string.IsNullOrEmpty(batch.RunId) ? current.RunId : batch.RunId, remoteAddress, now, current.Client ?? batch.Client, latestUi, Math.Max(0, batch.UiAgeMs), batch.Source == "worker" ? now : current.LastWorkerAt, lastDistinctUiAt, lastUiSequence, pageEnded, errorCount, timeline, events, inputTrace);
            TelemetryStoreSessionStateState observed = Interlocked.CompareExchange(ref _state, replacement, current);
            if (ReferenceEquals(observed, current))
            {
                return;
            }

            current = observed;
        }
    }

    public TelemetrySessionSummary GetSummary(DateTimeOffset now) => BuildSummary(Volatile.Read(ref _state), now);
    public TelemetrySessionDetail GetDetail(DateTimeOffset now)
    {
        TelemetryStoreSessionStateState state = Volatile.Read(ref _state);
        return new TelemetrySessionDetail(BuildSummary(state, now), state.Client, state.Timeline, state.Events);
    }

    public TelemetryInputTraceDocument GetInputTrace()
    {
        TelemetryStoreSessionStateState state = Volatile.Read(ref _state);
        return new TelemetryInputTraceDocument(
            SessionId,
            state.RunId,
            StartedAt,
            state.Client,
            TelemetryInputTraceDocument.CreateFieldNames(),
            TelemetryInputTraceDocument.CreateKindNames(),
            state.InputTrace);
    }

    public bool TryGetStatusTransition(DateTimeOffset now, out TelemetryStatusTransition transition)
    {
        while (true)
        {
            TelemetryStoreSessionStateState state = Volatile.Read(ref _state);
            var summary = BuildSummary(state, now);
            string previous = Volatile.Read(ref _lastReportedStatus);
            if (summary.Status == previous)
            {
                transition = null!;
                return false;
            }

            if (ReferenceEquals(Interlocked.CompareExchange(ref _lastReportedStatus, summary.Status, previous), previous))
            {
                transition = new TelemetryStatusTransition(SessionId, state.RunId, previous, summary.Status, summary.Signals, summary.WorkerAgeMs, summary.UiAgeMs);
                return true;
            }
        }
    }

    private TelemetrySessionSummary BuildSummary(TelemetryStoreSessionStateState state, DateTimeOffset now)
    {
        var lastReceiveAgeMs = Age(now, state.LastReceivedAt);
        var workerAgeMs = state.LastWorkerAt == default ? -1 : Age(now, state.LastWorkerAt);
        var uiAgeMs = state.LatestUi is null ? -1 : Math.Max(state.LatestReportedUiAgeMs + lastReceiveAgeMs, Age(now, state.LastDistinctUiAt));
        var signals = BuildSignals(state.LatestUi, uiAgeMs);
        var status = Classify(state, workerAgeMs, uiAgeMs, signals);
        var (wasmGrowth, managedGrowth) = CalculateGrowth(state.Timeline, now);
        return new TelemetrySessionSummary(SessionId, state.RunId, state.RemoteAddress, status, signals, StartedAt, state.LastReceivedAt, lastReceiveAgeMs, workerAgeMs, uiAgeMs, state.ErrorCount, state.LatestUi, wasmGrowth, managedGrowth);
    }

    private static string[] BuildSignals(TelemetryUiSnapshot? ui, double uiAgeMs)
    {
        var signals = new List<string>(4);
        if (ui is null)
        {
            return signals.ToArray();
        }

        if (ui.WebGlContextLost)
        {
            signals.Add("webgl-context-lost");
        }

        if (ui.ManagedProbeStarted &&
            ui.Visibility is null or "visible" &&
            (ui.ManagedSampleAgeMs > UiStaleAfter.TotalMilliseconds || ui.ManagedDispatcherAgeMs > UiStaleAfter.TotalMilliseconds))
        {
            signals.Add("managed-dispatcher-stalled");
        }

        if (ui.ActivePointers > 0 && ui.PointerMoveSinceLast > 0 && ui.CanvasFound && ui.CanvasPresentSinceLast == 0 && ui.CanvasPresentAgeMs > 2_000 && uiAgeMs < UiStaleAfter.TotalMilliseconds)
        {
            signals.Add("render-presentation-stalled");
        }

        if (ui.WasmMemoryMaxBytes > 0 && ui.WasmMemoryBytes / ui.WasmMemoryMaxBytes >= 0.9)
        {
            signals.Add("wasm-memory-near-limit");
        }

        return signals.ToArray();
    }

    private static string Classify(TelemetryStoreSessionStateState state, double workerAgeMs, double uiAgeMs, string[] signals)
    {
        var visible = state.LatestUi?.Visibility is null or "visible";
        if (workerAgeMs < 0)
        {
            return "starting";
        }

        if (workerAgeMs > WorkerStaleAfter.TotalMilliseconds)
        {
            return state.PageEnded || !visible ? "inactive" : "page-or-network-lost";
        }

        if (state.LatestUi is null)
        {
            return "starting";
        }

        if (uiAgeMs > UiStaleAfter.TotalMilliseconds)
        {
            return !visible ? "backgrounded" : "ui-thread-stalled";
        }

        if (signals.Contains("webgl-context-lost", StringComparer.Ordinal))
        {
            return "webgl-context-lost";
        }

        if (signals.Contains("managed-dispatcher-stalled", StringComparer.Ordinal))
        {
            return "managed-dispatcher-stalled";
        }

        if (signals.Contains("render-presentation-stalled", StringComparer.Ordinal))
        {
            return "render-presentation-stalled";
        }

        if (signals.Contains("wasm-memory-near-limit", StringComparer.Ordinal))
        {
            return "memory-pressure";
        }

        return "healthy";
    }

    private static (double Wasm, double Managed) CalculateGrowth(TelemetryTimelineSample[] timeline, DateTimeOffset now)
    {
        if (timeline.Length < 2)
        {
            return (0, 0);
        }

        var latest = timeline[^1];
        var cutoff = now - TimeSpan.FromSeconds(10);
        var baseline = timeline[0];
        foreach (var sample in timeline)
        {
            if (sample.ReceivedAt >= cutoff)
            {
                baseline = sample;
                break;
            }
        }

        return (Math.Max(0, latest.WasmMemoryBytes - baseline.WasmMemoryBytes), Math.Max(0, latest.ManagedHeapBytes - baseline.ManagedHeapBytes));
    }

    private static T[] AppendCapped<T>(T[] current, T item, int capacity)
    {
        int retained = Math.Min(current.Length, capacity - 1);
        var result = new T[retained + 1];
        if (retained > 0)
        {
            Array.Copy(current, current.Length - retained, result, 0, retained);
        }

        result[^1] = item;
        return result;
    }

    private static TelemetryEventView[] AppendEvents(TelemetryEventView[] current, List<TelemetryClientEvent>? incoming, DateTimeOffset now, ref int errorCount, ref bool pageEnded)
    {
        if (incoming is not { Count: > 0 })
        {
            return current;
        }

        int appended = Math.Min(incoming.Count, MaximumEvents);
        int retained = Math.Min(current.Length, MaximumEvents - appended);
        var result = new TelemetryEventView[retained + appended];
        if (retained > 0)
        {
            Array.Copy(current, current.Length - retained, result, 0, retained);
        }

        int incomingStart = incoming.Count - appended;
        for (int index = 0; index < appended; index++)
        {
            TelemetryClientEvent item = incoming[incomingStart + index];
            var kind = item.Kind ?? string.Empty;
            if (kind is "error" or "unhandled-rejection" or "worker-error" or "resource-error" or "webgl-context-lost")
            {
                errorCount++;
            }

            if (kind is "pagehide" or "freeze")
            {
                pageEnded = true;
            }
            else if (kind is "pageshow" or "resume" or "visibility-visible")
            {
                pageEnded = false;
            }

            result[retained + index] = new TelemetryEventView(now, item.AtUnixMs, item.UptimeMs, kind, item.Detail ?? string.Empty);
        }

        return result;
    }

    private static TelemetryInputTraceChunk[] AppendInputTrace(TelemetryInputTraceChunk[] current, TelemetryBatch batch)
    {
        if (batch.InputTraceValues is not { Length: > 0 } values)
        {
            return current;
        }

        int count = values.Length / batch.InputTraceStride;
        long startSequence = batch.InputTraceStartSequence;
        if (current is [.., { } latest])
        {
            long latestEndSequence = latest.StartSequence + latest.Count - 1;
            long incomingEndSequence = startSequence + count - 1;
            if (incomingEndSequence <= latestEndSequence)
            {
                return current;
            }

            if (startSequence <= latestEndSequence)
            {
                int skippedRecords = checked((int)(latestEndSequence - startSequence + 1));
                int retainedRecords = count - skippedRecords;
                var retainedValues = new double[retainedRecords * batch.InputTraceStride];
                Array.Copy(values, skippedRecords * batch.InputTraceStride, retainedValues, 0, retainedValues.Length);
                values = retainedValues;
                count = retainedRecords;
                startSequence = latestEndSequence + 1;
            }
        }

        var chunk = new TelemetryInputTraceChunk(startSequence, batch.InputTraceStride, count, batch.InputTraceDropped, values);
        return AppendCapped(current, chunk, MaximumInputTraceChunks);
    }

    private static double Age(DateTimeOffset now, DateTimeOffset then) => Math.Max(0, (now - then).TotalMilliseconds);
}
