namespace CalcManagerManaged.Interop;

/// <summary>
/// Calculator history item
/// </summary>
internal sealed class CalcHistoryItem
{
    public string Expression { get; set; } = string.Empty;

    public string Result { get; set; } = string.Empty;
}
