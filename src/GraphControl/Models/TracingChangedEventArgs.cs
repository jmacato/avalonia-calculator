namespace GraphControl;

public sealed class TracingChangedEventArgs(bool isTracing) : EventArgs
{
    public bool IsTracing { get; } = isTracing;
}
