using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;

namespace Calculator.BrowserHost;

internal static class TelemetryJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(ConfigureModelCreation);
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            TypeInfoResolver = resolver
        };
    }

    private static void ConfigureModelCreation(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type == typeof(TelemetryBatch))
        {
            typeInfo.CreateObject = static () => new TelemetryBatch();
        }
        else if (typeInfo.Type == typeof(TelemetryClientEvent))
        {
            typeInfo.CreateObject = static () => new TelemetryClientEvent();
        }
        else if (typeInfo.Type == typeof(TelemetryClientInfo))
        {
            typeInfo.CreateObject = static () => new TelemetryClientInfo();
        }
        else if (typeInfo.Type == typeof(TelemetryUiSnapshot))
        {
            typeInfo.CreateObject = static () => new TelemetryUiSnapshot();
        }
    }
}
