using System.Text.Json.Serialization;

namespace CalculatorApp.Automation;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, GenerationMode = JsonSourceGenerationMode.Default)]
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
