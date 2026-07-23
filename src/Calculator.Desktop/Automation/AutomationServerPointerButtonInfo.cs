using Avalonia.Input;

namespace CalculatorApp.Automation;

internal readonly record struct AutomationServerPointerButtonInfo(RawInputModifiers Modifier, PointerUpdateKind PressedKind, PointerUpdateKind ReleasedKind, MouseButton MouseButton);
