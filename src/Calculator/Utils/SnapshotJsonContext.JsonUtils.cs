using System.Text.Json.Serialization;

namespace CalculatorApp.JsonUtils;

[JsonSerializable(typeof(ApplicationSnapshotAlias))]
internal sealed partial class SnapshotJsonContext : JsonSerializerContext;
