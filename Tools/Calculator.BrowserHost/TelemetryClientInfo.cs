namespace Calculator.BrowserHost;

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
