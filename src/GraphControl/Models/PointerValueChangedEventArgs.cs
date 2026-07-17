using Avalonia;

namespace GraphControl;

public sealed class PointerValueChangedEventArgs(Point value) : EventArgs
{
    public Point Value { get; } = value;
}
