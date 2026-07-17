using System.Text.Json.Serialization;

namespace CalculatorApp.Automation;

internal sealed record AutomationKeyRequest(string Key, string? Modifiers, string? Text);
