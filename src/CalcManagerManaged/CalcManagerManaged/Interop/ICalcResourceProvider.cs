namespace CalcManagerManaged.Interop;

/// <summary>
/// Interface for the resource provider
/// </summary>
internal interface ICalcResourceProvider
{
    string? GetString(string resourceId);
}
