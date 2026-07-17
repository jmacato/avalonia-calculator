namespace GraphControl;

public sealed class TracingValueChangedEventArgs(double x, double y) : EventArgs
{
    public double X { get; } = x;

    public double Y { get; } = y;
}
