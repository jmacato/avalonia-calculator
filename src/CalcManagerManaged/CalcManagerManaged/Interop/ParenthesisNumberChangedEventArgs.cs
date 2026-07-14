namespace CalcManagerManaged.Interop;

/// <summary>
/// Event data for the parenthesis number changed event
/// </summary>
internal sealed class ParenthesisNumberChangedEventArgs : EventArgs
{
    public ParenthesisNumberChangedEventArgs(uint count)
    {
        Count = count;
    }

    public uint Count { get; }
}
