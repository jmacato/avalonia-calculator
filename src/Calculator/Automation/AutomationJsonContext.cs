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
    string AccessibilityView,
    int HeadingLevel,
    string? LandmarkType,
    double X,
    double Y,
    double Width,
    double Height,
    double DesiredWidth,
    double DesiredHeight,
    bool IsVisible,
    bool IsEnabled,
    bool IsFocused,
    double Opacity,
    double? FontSize,
    bool? IsActive,
    bool? IsDropDownOpen,
    bool? IsPopupOpen,
    int? SelectedIndex,
    double? ScrollOffsetX,
    double? ScrollOffsetY,
    double? ScrollExtentWidth,
    double? ScrollExtentHeight,
    double? ScrollViewportWidth,
    double? ScrollViewportHeight,
    double? PopupOffsetX,
    double? PopupOffsetY,
    string? Text,
    string? Classes);

internal sealed record AutomationPointerRequest(
    double X,
    double Y,
    string Kind,
    string? Modifiers,
    string? Button,
    string? PointerType,
    double? DeltaX,
    double? DeltaY);

internal sealed record AutomationTargetRequest(string Target);

internal sealed record AutomationKeyRequest(
    string Key,
    string? Modifiers,
    string? Text);

internal sealed record AutomationTextRequest(string Text);

internal sealed record AutomationWindowSizeRequest(double Width, double Height);
