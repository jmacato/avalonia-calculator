namespace Calculator.BrowserHost;

internal sealed class TelemetryClientEvent
{
    public string? Kind { get; set; }
    public long AtUnixMs { get; set; }
    public double UptimeMs { get; set; }
    public string? Detail { get; set; }
}
