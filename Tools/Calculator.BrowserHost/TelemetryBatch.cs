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
    public long InputTraceStartSequence { get; set; }
    public int InputTraceStride { get; set; }
    public long InputTraceDropped { get; set; }
    public double[]? InputTraceValues { get; set; }
}
