namespace Calculator.BrowserHost;

internal sealed record TelemetrySessionSummary(string SessionId, string RunId, string RemoteAddress, string Status, string[] Signals, DateTimeOffset StartedAt, DateTimeOffset LastReceivedAt, double LastReceiveAgeMs, double WorkerAgeMs, double UiAgeMs, int ErrorCount, TelemetryUiSnapshot? Latest, double WasmGrowthBytes10s);
