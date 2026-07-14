namespace CalcManagerManaged.Interop;

/// <summary>
/// Event data for the error state changed event
/// </summary>
internal sealed class IsInErrorChangedEventArgs : EventArgs
{
    public IsInErrorChangedEventArgs(bool isError)
    {
        IsError = isError;
    }

    public bool IsError { get; }
}
