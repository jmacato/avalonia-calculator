namespace CalcManagerManaged.Interop;

/// <summary>
/// Event data for the history item added event
/// </summary>
internal sealed class HistoryItemAddedEventArgs : EventArgs
{
    public HistoryItemAddedEventArgs(uint addedItemIndex)
    {
        AddedItemIndex = addedItemIndex;
    }

    public uint AddedItemIndex { get; }
}
