namespace CalcManagerManaged.Interop;

/// <summary>
/// Event data for the display changed event
/// </summary>
internal sealed class DisplayChangedEventArgs : EventArgs
{
    public DisplayChangedEventArgs(string displayText, bool isError)
    {
        DisplayText = displayText;
        IsError = isError;
    }

    public string DisplayText { get; }

    public bool IsError { get; }
}
