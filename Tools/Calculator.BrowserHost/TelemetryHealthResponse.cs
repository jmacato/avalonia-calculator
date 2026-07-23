namespace Calculator.BrowserHost;

internal sealed record TelemetryHealthResponse(
    string Status,
    int Sessions,
    string LogPath,
    DateTimeOffset ServerTime);
