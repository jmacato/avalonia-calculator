using System;

namespace GraphControl;

public sealed class GraphViewChangedEventArgs : EventArgs
{
    public GraphViewChangedEventArgs(GraphViewChangedReason reason)
    {
        Reason = reason;
    }

    public GraphViewChangedReason Reason { get; }
}
