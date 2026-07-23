namespace Calculator.BrowserHost;

internal sealed record TelemetryInputTraceChunk(long StartSequence, int Stride, int Count, long DroppedBefore, double[] Values);
