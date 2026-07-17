using System.Text.Json.Serialization;

namespace CalculatorApp.Automation;

internal sealed record AutomationHealthResponse(string Status, int ProcessId, int Port);
