namespace Calculator.BrowserHost;

internal sealed record TelemetryStatusTransition(string SessionId, string RunId, string PreviousStatus, string Status, string[] Signals, double WorkerAgeMs, double UiAgeMs);
