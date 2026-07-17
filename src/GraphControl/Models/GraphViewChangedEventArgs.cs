namespace GraphControl;

public sealed class GraphViewChangedEventArgs(GraphViewChangedReason reason) : EventArgs
{
    public GraphViewChangedReason Reason { get; } = reason;
}
