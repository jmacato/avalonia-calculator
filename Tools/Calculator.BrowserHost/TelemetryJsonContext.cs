using System.Text.Json.Serialization;

namespace Calculator.BrowserHost;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = false)]
[JsonSerializable(typeof(TelemetryBatch))]
[JsonSerializable(typeof(TelemetryLogEnvelope))]
[JsonSerializable(typeof(TelemetrySessionSummary[]))]
[JsonSerializable(typeof(TelemetrySessionDetail))]
[JsonSerializable(typeof(TelemetryInputTraceDocument))]
[JsonSerializable(typeof(TelemetryErrorResponse))]
[JsonSerializable(typeof(TelemetryHealthResponse))]
[JsonSerializable(typeof(TelemetryClearSessionsResponse))]
internal sealed partial class TelemetryJsonContext : JsonSerializerContext;
