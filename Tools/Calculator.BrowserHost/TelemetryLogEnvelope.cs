namespace Calculator.BrowserHost;

internal sealed record TelemetryLogEnvelope(DateTimeOffset ReceivedAt, string RemoteAddress, TelemetryBatch Batch);
