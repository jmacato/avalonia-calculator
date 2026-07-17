namespace Calculator.BrowserHost;

internal sealed record TelemetrySessionDetail(TelemetrySessionSummary Summary, TelemetryClientInfo? Client, TelemetryTimelineSample[] Timeline, TelemetryEventView[] Events);
