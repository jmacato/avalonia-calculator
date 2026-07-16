namespace Calculator.BrowserHost;

internal sealed class TelemetryBatch
{
    public int Version { get; set; }
    public string? SessionId { get; set; }
    public string? RunId { get; set; }
    public string? Source { get; set; }
    public long WorkerSequence { get; set; }
    public long SentAtUnixMs { get; set; }
    public double WorkerUptimeMs { get; set; }
    public int WorkerUploadFailures { get; set; }
    public int SkippedUploads { get; set; }
    public double UiAgeMs { get; set; }
    public TelemetryClientInfo? Client { get; set; }
    public TelemetryUiSnapshot? Ui { get; set; }
    public List<TelemetryClientEvent>? Events { get; set; }
}

internal sealed class TelemetryClientInfo
{
    public long StartedAtUnixMs { get; set; }
    public string? UserAgent { get; set; }
    public string? Platform { get; set; }
    public string? Language { get; set; }
    public int HardwareConcurrency { get; set; }
    public double DeviceMemoryGiB { get; set; }
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public int ScreenPixelDepth { get; set; }
    public bool CrossOriginIsolated { get; set; }
    public string? Page { get; set; }
}

internal sealed class TelemetryUiSnapshot
{
    public long Sequence { get; set; }
    public long CapturedAtUnixMs { get; set; }
    public double UptimeMs { get; set; }
    public string? BootStage { get; set; }
    public string? Visibility { get; set; }
    public bool HasFocus { get; set; }

    public long FrameCountTotal { get; set; }
    public int FramesSinceLast { get; set; }
    public double FrameRate { get; set; }
    public double MaxFrameGapMs { get; set; }
    public double MaxFrameGapTotalMs { get; set; }
    public long LongFrameCountTotal { get; set; }
    public long LongTaskCountTotal { get; set; }
    public double LongTaskDurationSinceLastMs { get; set; }
    public double MaxLongTaskMs { get; set; }

    public long PointerDownTotal { get; set; }
    public long PointerMoveTotal { get; set; }
    public int PointerMoveSinceLast { get; set; }
    public long PointerUpTotal { get; set; }
    public long PointerCancelTotal { get; set; }
    public long WheelTotal { get; set; }
    public int WheelSinceLast { get; set; }
    public int ActivePointers { get; set; }
    public double LastPointerAgeMs { get; set; }
    public long GotPointerCaptureTotal { get; set; }
    public long LostPointerCaptureTotal { get; set; }

    public bool CanvasFound { get; set; }
    public int CanvasWidth { get; set; }
    public int CanvasHeight { get; set; }
    public double CanvasCssWidth { get; set; }
    public double CanvasCssHeight { get; set; }
    public long CanvasPresentTotal { get; set; }
    public int CanvasPresentSinceLast { get; set; }
    public double CanvasPresentAgeMs { get; set; }
    public bool WebGlContextLost { get; set; }
    public long WebGlContextLosses { get; set; }
    public long WebGlContextRestores { get; set; }

    public long KeyDownTotal { get; set; }
    public long BeforeInputTotal { get; set; }
    public long InputTotal { get; set; }
    public long SelectionChangeTotal { get; set; }
    public long ContextMenuTotal { get; set; }
    public long FocusInTotal { get; set; }
    public long FocusOutTotal { get; set; }
    public string? ActiveElement { get; set; }
    public bool NativeInputFocused { get; set; }
    public int NativeInputValueLength { get; set; }
    public int NativeSelectionStart { get; set; }
    public int NativeSelectionEnd { get; set; }

    public double VisualViewportWidth { get; set; }
    public double VisualViewportHeight { get; set; }
    public double VisualViewportScale { get; set; }
    public double VisualViewportOffsetTop { get; set; }
    public int InnerWidth { get; set; }
    public int InnerHeight { get; set; }
    public double DevicePixelRatio { get; set; }

    public double WasmMemoryBytes { get; set; }
    public double WasmMemoryMaxBytes { get; set; }
    public double JsHeapBytes { get; set; }
    public double ManagedHeapBytes { get; set; }
    public double ManagedAllocatedBytes { get; set; }
    public int ManagedGen0Collections { get; set; }
    public int ManagedGen1Collections { get; set; }
    public int ManagedGen2Collections { get; set; }
    public bool ManagedProbeStarted { get; set; }
    public bool ManagedProbeInFlight { get; set; }
    public double ManagedSampleAgeMs { get; set; }
    public double ManagedDispatcherPulse { get; set; }
    public double ManagedDispatcherAgeMs { get; set; }
}

internal sealed class TelemetryClientEvent
{
    public string? Kind { get; set; }
    public long AtUnixMs { get; set; }
    public double UptimeMs { get; set; }
    public string? Detail { get; set; }
}

internal sealed record TelemetryLogEnvelope(
    DateTimeOffset ReceivedAt,
    string RemoteAddress,
    TelemetryBatch Batch);

internal sealed record TelemetryTimelineSample(
    DateTimeOffset ReceivedAt,
    long WorkerSequence,
    double WorkerUptimeMs,
    int WorkerUploadFailures,
    int SkippedUploads,
    double UiAgeMs,
    long UiSequence,
    string BootStage,
    string Visibility,
    bool HasFocus,
    double FrameRate,
    double MaxFrameGapMs,
    double MaxFrameGapTotalMs,
    long LongFrameCountTotal,
    long LongTaskCountTotal,
    double MaxLongTaskMs,
    int ActivePointers,
    int PointerMoveSinceLast,
    double LastPointerAgeMs,
    long LostPointerCaptureTotal,
    int CanvasPresentSinceLast,
    double CanvasPresentAgeMs,
    bool WebGlContextLost,
    long WebGlContextLosses,
    string ActiveElement,
    bool NativeInputFocused,
    long SelectionChangeTotal,
    long ContextMenuTotal,
    double VisualViewportHeight,
    int InnerHeight,
    int CanvasWidth,
    int CanvasHeight,
    double WasmMemoryBytes,
    double WasmMemoryMaxBytes,
    double JsHeapBytes,
    double ManagedHeapBytes,
    double ManagedAllocatedBytes,
    int ManagedGen0Collections,
    int ManagedGen1Collections,
    int ManagedGen2Collections,
    bool ManagedProbeStarted,
    bool ManagedProbeInFlight,
    double ManagedSampleAgeMs,
    double ManagedDispatcherPulse,
    double ManagedDispatcherAgeMs);

internal sealed record TelemetryEventView(
    DateTimeOffset ReceivedAt,
    long AtUnixMs,
    double UptimeMs,
    string Kind,
    string Detail);

internal sealed record TelemetrySessionSummary(
    string SessionId,
    string RunId,
    string RemoteAddress,
    string Status,
    string[] Signals,
    DateTimeOffset StartedAt,
    DateTimeOffset LastReceivedAt,
    double LastReceiveAgeMs,
    double WorkerAgeMs,
    double UiAgeMs,
    int ErrorCount,
    TelemetryUiSnapshot? Latest,
    double WasmGrowthBytes10s,
    double ManagedGrowthBytes10s);

internal sealed record TelemetrySessionDetail(
    TelemetrySessionSummary Summary,
    TelemetryClientInfo? Client,
    TelemetryTimelineSample[] Timeline,
    TelemetryEventView[] Events);

internal sealed record TelemetryStatusTransition(
    string SessionId,
    string RunId,
    string PreviousStatus,
    string Status,
    string[] Signals,
    double WorkerAgeMs,
    double UiAgeMs);
