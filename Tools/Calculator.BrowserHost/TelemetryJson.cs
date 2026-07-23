using System.Text.Json;

namespace Calculator.BrowserHost;

internal static class TelemetryJson
{
    public static JsonSerializerOptions Options => TelemetryJsonContext.Default.Options;
}
