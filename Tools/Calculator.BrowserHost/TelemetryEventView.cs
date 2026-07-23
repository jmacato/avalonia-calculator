namespace Calculator.BrowserHost;

internal sealed record TelemetryEventView(DateTimeOffset ReceivedAt, long AtUnixMs, double UptimeMs, string Kind, string Detail);
