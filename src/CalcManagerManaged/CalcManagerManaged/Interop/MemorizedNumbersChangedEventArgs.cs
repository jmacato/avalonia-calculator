namespace CalcManagerManaged.Interop;

/// <summary>
/// Event data for the memorized numbers changed event
/// </summary>
internal sealed class MemorizedNumbersChangedEventArgs : EventArgs
{
    public MemorizedNumbersChangedEventArgs(IReadOnlyList<string> memorizedNumbers)
    {
        MemorizedNumbers = memorizedNumbers;
    }

    public IReadOnlyList<string> MemorizedNumbers { get; }
}
