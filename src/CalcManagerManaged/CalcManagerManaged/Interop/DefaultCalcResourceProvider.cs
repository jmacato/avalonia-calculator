using System.IO.Enumeration;
using CalcManagerManaged.Resources;

namespace CalcManagerManaged.Interop;

/// <summary>
/// Default implementation of ICalcResourceProvider
/// </summary>
public class DefaultCalcResourceProvider : ICalcResourceProvider
{
    private Dictionary<string, string> _resources;

    public DefaultCalcResourceProvider()
    {
        // Initialize with some default resources
        _resources = DefaultCombinedCalcResource.StringResources;
    }

    public string GetString(string resourceId)
    {
        if (_resources.TryGetValue(resourceId, out string value))
        {
            return value;
        }

        System.Diagnostics.Debug.WriteLine($"CalcManager resource not found: {resourceId}");
        // Return the resource ID if not found
        return resourceId;
    }
}
