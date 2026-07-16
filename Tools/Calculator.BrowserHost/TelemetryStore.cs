using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace Calculator.BrowserHost;

internal sealed class TelemetryStore
{
    private const int MaximumTimelineSamples = 1_200;
    private const int MaximumEvents = 512;
    private static readonly TimeSpan WorkerStaleAfter = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan UiStaleAfter = TimeSpan.FromSeconds(4);

    private readonly ConcurrentDictionary<string, SessionState> _sessions = new(StringComparer.Ordinal);
    private readonly Channel<TelemetryLogEnvelope> _logChannel = Channel.CreateBounded<TelemetryLogEnvelope>(
        new BoundedChannelOptions(4_096)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public ChannelReader<TelemetryLogEnvelope> LogEntries => _logChannel.Reader;

    public bool TryIngest(TelemetryBatch batch, string remoteAddress, out string? error)
    {
        error = ValidateAndSanitize(batch);
        if (error is not null)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var session = _sessions.GetOrAdd(
            batch.SessionId!,
            static (id, state) => new SessionState(id, state.RunId!, state.RemoteAddress, state.Now),
            (RunId: batch.RunId, RemoteAddress: remoteAddress, Now: now));

        session.Ingest(batch, remoteAddress, now);
        _logChannel.Writer.TryWrite(new TelemetryLogEnvelope(now, remoteAddress, batch));
        return true;
    }

    public TelemetrySessionSummary[] GetSummaries(DateTimeOffset now) =>
        _sessions.Values
            .Select(session => session.GetSummary(now))
            .OrderByDescending(summary => summary.LastReceivedAt)
            .ToArray();

    public TelemetrySessionDetail? GetDetail(string sessionId, DateTimeOffset now) =>
        _sessions.TryGetValue(sessionId, out var session) ? session.GetDetail(now) : null;

    public TelemetryStatusTransition[] FindStatusTransitions(DateTimeOffset now)
    {
        var transitions = new List<TelemetryStatusTransition>();
        foreach (var session in _sessions.Values)
        {
            if (session.TryGetStatusTransition(now, out var transition))
            {
                transitions.Add(transition);
            }
        }

        return transitions.ToArray();
    }

    private static string? ValidateAndSanitize(TelemetryBatch batch)
    {
        if (batch.Version != 1)
        {
            return "Unsupported telemetry version.";
        }

        if (!IsSessionId(batch.SessionId))
        {
            return "Invalid session id.";
        }

        if (batch.WorkerSequence < 0 || batch.WorkerUploadFailures < 0 || batch.SkippedUploads < 0)
        {
            return "Invalid counter value.";
        }

        batch.RunId = Truncate(batch.RunId, 96);
        batch.Source = Truncate(batch.Source, 24);
        if (batch.Source is not ("worker" or "ui-beacon"))
        {
            return "Invalid telemetry source.";
        }

        if (batch.Client is { } client)
        {
            client.UserAgent = Truncate(client.UserAgent, 384);
            client.Platform = Truncate(client.Platform, 96);
            client.Language = Truncate(client.Language, 48);
            client.Page = Truncate(client.Page, 384);
        }

        if (batch.Ui is { } ui)
        {
            ui.BootStage = Truncate(ui.BootStage, 80);
            ui.Visibility = Truncate(ui.Visibility, 32);
            ui.ActiveElement = Truncate(ui.ActiveElement, 192);
        }

        if (batch.Events is { Count: > 64 })
        {
            return "Too many events in one batch.";
        }

        if (batch.Events is not null)
        {
            foreach (var item in batch.Events)
            {
                item.Kind = Truncate(item.Kind, 64);
                item.Detail = Truncate(item.Detail, 768);
            }
        }

        return null;
    }

    private static bool IsSessionId(string? value)
    {
        if (value is null || value.Length is < 8 or > 80)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static string Truncate(string? value, int maximumLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maximumLength ? value : value[..maximumLength];
    }

    private sealed class SessionState
    {
        private readonly object _gate = new();
        private readonly Queue<TelemetryTimelineSample> _timeline = new();
        private readonly Queue<TelemetryEventView> _events = new();
        private string _lastReportedStatus = string.Empty;
        private DateTimeOffset _lastWorkerAt;
        private DateTimeOffset _lastDistinctUiAt;
        private long _lastUiSequence = -1;
        private bool _pageEnded;
        private int _errorCount;

        public SessionState(string sessionId, string runId, string remoteAddress, DateTimeOffset now)
        {
            SessionId = sessionId;
            RunId = runId;
            RemoteAddress = remoteAddress;
            StartedAt = now;
            LastReceivedAt = now;
        }

        public string SessionId { get; }
        public string RunId { get; private set; }
        public string RemoteAddress { get; private set; }
        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset LastReceivedAt { get; private set; }
        public TelemetryClientInfo? Client { get; private set; }
        public TelemetryUiSnapshot? LatestUi { get; private set; }
        public double LatestReportedUiAgeMs { get; private set; }

        public void Ingest(TelemetryBatch batch, string remoteAddress, DateTimeOffset now)
        {
            lock (_gate)
            {
                LastReceivedAt = now;
                RemoteAddress = remoteAddress;
                if (!string.IsNullOrEmpty(batch.RunId))
                {
                    RunId = batch.RunId;
                }

                if (batch.Source == "worker")
                {
                    _lastWorkerAt = now;
                }

                Client ??= batch.Client;
                LatestReportedUiAgeMs = Math.Max(0, batch.UiAgeMs);

                if (batch.Ui is { } ui)
                {
                    LatestUi = ui;
                    if (ui.Sequence != _lastUiSequence)
                    {
                        _lastUiSequence = ui.Sequence;
                        _lastDistinctUiAt = now;
                    }

                    EnqueueTimeline(new TelemetryTimelineSample(
                        now,
                        batch.WorkerSequence,
                        batch.WorkerUptimeMs,
                        batch.WorkerUploadFailures,
                        batch.SkippedUploads,
                        batch.UiAgeMs,
                        ui.Sequence,
                        ui.BootStage ?? string.Empty,
                        ui.Visibility ?? string.Empty,
                        ui.HasFocus,
                        ui.FrameRate,
                        ui.MaxFrameGapMs,
                        ui.MaxFrameGapTotalMs,
                        ui.LongFrameCountTotal,
                        ui.LongTaskCountTotal,
                        ui.MaxLongTaskMs,
                        ui.ActivePointers,
                        ui.PointerMoveSinceLast,
                        ui.LastPointerAgeMs,
                        ui.LostPointerCaptureTotal,
                        ui.CanvasPresentSinceLast,
                        ui.CanvasPresentAgeMs,
                        ui.WebGlContextLost,
                        ui.WebGlContextLosses,
                        ui.ActiveElement ?? string.Empty,
                        ui.NativeInputFocused,
                        ui.SelectionChangeTotal,
                        ui.ContextMenuTotal,
                        ui.VisualViewportHeight,
                        ui.InnerHeight,
                        ui.CanvasWidth,
                        ui.CanvasHeight,
                        ui.WasmMemoryBytes,
                        ui.WasmMemoryMaxBytes,
                        ui.JsHeapBytes,
                        ui.ManagedHeapBytes,
                        ui.ManagedAllocatedBytes,
                        ui.ManagedGen0Collections,
                        ui.ManagedGen1Collections,
                        ui.ManagedGen2Collections,
                        ui.ManagedProbeStarted,
                        ui.ManagedProbeInFlight,
                        ui.ManagedSampleAgeMs,
                        ui.ManagedDispatcherPulse,
                        ui.ManagedDispatcherAgeMs));
                }

                if (batch.Events is not null)
                {
                    foreach (var item in batch.Events)
                    {
                        var kind = item.Kind ?? string.Empty;
                        if (kind is "error" or "unhandled-rejection" or "worker-error" or "resource-error" or "webgl-context-lost")
                        {
                            _errorCount++;
                        }

                        if (kind is "pagehide" or "freeze")
                        {
                            _pageEnded = true;
                        }
                        else if (kind is "pageshow" or "resume" or "visibility-visible")
                        {
                            _pageEnded = false;
                        }

                        _events.Enqueue(new TelemetryEventView(
                            now,
                            item.AtUnixMs,
                            item.UptimeMs,
                            kind,
                            item.Detail ?? string.Empty));
                        while (_events.Count > MaximumEvents)
                        {
                            _events.Dequeue();
                        }
                    }
                }
            }
        }

        public TelemetrySessionSummary GetSummary(DateTimeOffset now)
        {
            lock (_gate)
            {
                return BuildSummary(now);
            }
        }

        public TelemetrySessionDetail GetDetail(DateTimeOffset now)
        {
            lock (_gate)
            {
                return new TelemetrySessionDetail(
                    BuildSummary(now),
                    Client,
                    _timeline.ToArray(),
                    _events.ToArray());
            }
        }

        public bool TryGetStatusTransition(DateTimeOffset now, out TelemetryStatusTransition transition)
        {
            lock (_gate)
            {
                var summary = BuildSummary(now);
                if (summary.Status == _lastReportedStatus)
                {
                    transition = null!;
                    return false;
                }

                transition = new TelemetryStatusTransition(
                    SessionId,
                    RunId,
                    _lastReportedStatus,
                    summary.Status,
                    summary.Signals,
                    summary.WorkerAgeMs,
                    summary.UiAgeMs);
                _lastReportedStatus = summary.Status;
                return true;
            }
        }

        private TelemetrySessionSummary BuildSummary(DateTimeOffset now)
        {
            var lastReceiveAgeMs = Age(now, LastReceivedAt);
            var workerAgeMs = _lastWorkerAt == default ? -1 : Age(now, _lastWorkerAt);
            var uiAgeMs = LatestUi is null
                ? -1
                : Math.Max(LatestReportedUiAgeMs + lastReceiveAgeMs, Age(now, _lastDistinctUiAt));
            var signals = BuildSignals(uiAgeMs);
            var status = Classify(workerAgeMs, uiAgeMs, signals);
            var (wasmGrowth, managedGrowth) = CalculateGrowth(now);

            return new TelemetrySessionSummary(
                SessionId,
                RunId,
                RemoteAddress,
                status,
                signals,
                StartedAt,
                LastReceivedAt,
                lastReceiveAgeMs,
                workerAgeMs,
                uiAgeMs,
                _errorCount,
                LatestUi,
                wasmGrowth,
                managedGrowth);
        }

        private string[] BuildSignals(double uiAgeMs)
        {
            var signals = new List<string>(4);
            var ui = LatestUi;
            if (ui is null)
            {
                return signals.ToArray();
            }

            if (ui.WebGlContextLost)
            {
                signals.Add("webgl-context-lost");
            }

            if (ui.ManagedProbeStarted &&
                (ui.ManagedSampleAgeMs > UiStaleAfter.TotalMilliseconds ||
                 ui.ManagedDispatcherAgeMs > UiStaleAfter.TotalMilliseconds))
            {
                signals.Add("managed-dispatcher-stalled");
            }

            if (ui.ActivePointers > 0 && ui.PointerMoveSinceLast > 0 && ui.CanvasFound &&
                ui.CanvasPresentSinceLast == 0 && ui.CanvasPresentAgeMs > 2_000 && uiAgeMs < UiStaleAfter.TotalMilliseconds)
            {
                signals.Add("render-presentation-stalled");
            }

            if (ui.WasmMemoryMaxBytes > 0 && ui.WasmMemoryBytes / ui.WasmMemoryMaxBytes >= 0.9)
            {
                signals.Add("wasm-memory-near-limit");
            }

            return signals.ToArray();
        }

        private string Classify(double workerAgeMs, double uiAgeMs, string[] signals)
        {
            var visible = LatestUi?.Visibility is null or "visible";
            if (workerAgeMs < 0)
            {
                return "starting";
            }

            if (workerAgeMs > WorkerStaleAfter.TotalMilliseconds)
            {
                return _pageEnded || !visible ? "inactive" : "page-or-network-lost";
            }

            if (LatestUi is null)
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

        private (double Wasm, double Managed) CalculateGrowth(DateTimeOffset now)
        {
            if (_timeline.Count < 2)
            {
                return (0, 0);
            }

            var latest = _timeline.Last();
            var cutoff = now - TimeSpan.FromSeconds(10);
            var baseline = _timeline.First();
            foreach (var sample in _timeline)
            {
                if (sample.ReceivedAt >= cutoff)
                {
                    baseline = sample;
                    break;
                }
            }

            return (
                Math.Max(0, latest.WasmMemoryBytes - baseline.WasmMemoryBytes),
                Math.Max(0, latest.ManagedHeapBytes - baseline.ManagedHeapBytes));
        }

        private void EnqueueTimeline(TelemetryTimelineSample sample)
        {
            _timeline.Enqueue(sample);
            while (_timeline.Count > MaximumTimelineSamples)
            {
                _timeline.Dequeue();
            }
        }

        private static double Age(DateTimeOffset now, DateTimeOffset then) =>
            Math.Max(0, (now - then).TotalMilliseconds);
    }
}

internal sealed class TelemetryLogWriter(
    TelemetryStore store,
    BrowserHostSettings settings,
    ILogger<TelemetryLogWriter> logger) : BackgroundService
{
    public string LogPath { get; } = Path.Combine(
        settings.TelemetryDirectory,
        $"browser-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.ndjson");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(settings.TelemetryDirectory);
        await using var stream = new FileStream(
            LogPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var writer = new StreamWriter(stream);

        try
        {
            await foreach (var entry in store.LogEntries.ReadAllAsync(stoppingToken))
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(entry, TelemetryJson.Options));
                await writer.FlushAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Telemetry log writer failed.");
        }
    }
}

internal sealed class TelemetryMonitor(
    TelemetryStore store,
    ILogger<TelemetryMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var transition in store.FindStatusTransitions(DateTimeOffset.UtcNow))
                {
                    logger.LogWarning(
                        "Telemetry {Session} ({Run}) {Previous} -> {Status}; worker age {WorkerAge:F0} ms, UI age {UiAge:F0} ms, signals [{Signals}]",
                        transition.SessionId,
                        transition.RunId,
                        string.IsNullOrEmpty(transition.PreviousStatus) ? "new" : transition.PreviousStatus,
                        transition.Status,
                        transition.WorkerAgeMs,
                        transition.UiAgeMs,
                        string.Join(", ", transition.Signals));
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}

internal static class TelemetryJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
