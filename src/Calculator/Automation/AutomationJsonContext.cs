using System.Text.Json.Serialization;

namespace CalculatorApp.Automation;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(AutomationHealthResponse))]
[JsonSerializable(typeof(AutomationActionResponse))]
[JsonSerializable(typeof(AutomationErrorResponse))]
[JsonSerializable(typeof(AutomationTreeResponse))]
[JsonSerializable(typeof(List<AutomationElementInfo>))]
[JsonSerializable(typeof(AutomationPointerRequest))]
[JsonSerializable(typeof(AutomationTargetRequest))]
[JsonSerializable(typeof(AutomationKeyRequest))]
[JsonSerializable(typeof(AutomationTextRequest))]
[JsonSerializable(typeof(AutomationWindowSizeRequest))]
internal sealed partial class AutomationJsonContext : JsonSerializerContext;

internal sealed record AutomationHealthResponse(string Status, int ProcessId, int Port);

internal sealed record AutomationActionResponse(string Status);

internal sealed record AutomationErrorResponse(string Error);

internal sealed record AutomationTreeResponse(List<AutomationElementInfo> Elements);

internal sealed record AutomationElementInfo(
    string Type,
    string? Name,
    string? AutomationId,
    string? AutomationName,
    double X,
    double Y,
    double Width,
    double Height,
    bool IsVisible,
    bool IsEnabled);

internal sealed record AutomationPointerRequest(
    double X,
    double Y,
    string Kind,
    string? Modifiers);

internal sealed record AutomationTargetRequest(string Target);

internal sealed record AutomationKeyRequest(
    string Key,
    string? Modifiers,
    string? Text);

internal sealed record AutomationTextRequest(string Text);

internal sealed record AutomationWindowSizeRequest(double Width, double Height);
