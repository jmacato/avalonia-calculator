namespace CalcManagerManaged.Interop;

/// <summary>
/// Interface for the resource provider
/// </summary>
public interface ICalcResourceProvider
{
    string GetString(string resourceId);
}
