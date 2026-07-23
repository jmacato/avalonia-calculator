namespace Calculator.BrowserHost;

internal sealed record TelemetryStoreSessionStateState(string RunId, string RemoteAddress, DateTimeOffset LastReceivedAt, TelemetryClientInfo? Client, TelemetryUiSnapshot? LatestUi, double LatestReportedUiAgeMs, DateTimeOffset LastWorkerAt, DateTimeOffset LastDistinctUiAt, long LastUiSequence, bool PageEnded, int ErrorCount, TelemetryTimelineSample[] Timeline, TelemetryEventView[] Events, TelemetryInputTraceChunk[] InputTrace);
