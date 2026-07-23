namespace CalculatorApp.Automation;

internal sealed record AutomationPointerRequest(double X, double Y, string Kind, string? Modifiers, string? Button, string? PointerType, double? DeltaX, double? DeltaY);
