namespace CalcManagerManaged.Interop;

/// <summary>
/// Event data for the expression display changed event
/// </summary>
internal sealed class ExpressionDisplayChangedEventArgs : EventArgs
{
    public ExpressionDisplayChangedEventArgs(IReadOnlyList<(string Text, int Type)> tokens)
    {
        Tokens = tokens;
    }

    public IReadOnlyList<(string Text, int Type)> Tokens { get; }
}
