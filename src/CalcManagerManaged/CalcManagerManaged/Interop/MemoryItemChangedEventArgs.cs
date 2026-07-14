namespace CalcManagerManaged.Interop;

/// <summary>
/// Event data for the memory item changed event
/// </summary>
internal sealed class MemoryItemChangedEventArgs : EventArgs
{
    public MemoryItemChangedEventArgs(uint memoryIndex)
    {
        MemoryIndex = memoryIndex;
    }

    public uint MemoryIndex { get; }
}
