using System.Text.Json.Serialization;

namespace CalculatorApp.Automation;

internal sealed record AutomationTreeResponse(List<AutomationElementInfo> Elements);
