namespace Calculator.BrowserHost;

internal sealed record TelemetryInputTraceDocument(string SessionId, string RunId, DateTimeOffset StartedAt, TelemetryClientInfo? Client, string[] FieldNames, string[] KindNames, TelemetryInputTraceChunk[] Chunks)
{
    internal const int Stride = 16;

    internal static string[] CreateFieldNames() =>
    [
        "uptimeMs", "kind", "pointerType", "pointerId",
        "clientX", "clientY", "button", "buttons",
        "modifiers", "pressure", "deltaX", "deltaY",
        "viewportWidth", "viewportHeight", "detail1", "detail2",
    ];

    internal static string[] CreateKindNames() =>
    [
        "none", "pointerdown", "pointermove", "pointerup", "pointercancel",
        "wheel", "keydown", "keyup", "beforeinput", "input", "focusin",
        "focusout", "resize", "visibility", "contextmenu",
    ];
}
