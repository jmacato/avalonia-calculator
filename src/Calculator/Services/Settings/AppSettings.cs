// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace CalculatorApp.Services.Settings;

public enum ConverterUnitDisplayMode
{
    Automatic,
    Left,
    Right,
    WindowsNative
}

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public ConverterUnitDisplayMode ConverterUnitDisplayMode { get; init; } =
        ConverterUnitDisplayMode.Automatic;

    public bool AutomaticCurrencyRefresh { get; init; } = true;

    public bool GraphThemeMatchApp { get; init; }

    public string UnitConverterPreferences { get; init; } = string.Empty;

    [JsonPropertyName("lastCurrencyFrom")]
    public string CurrencyUnitFrom { get; init; } = string.Empty;

    [JsonPropertyName("lastCurrencyTo")]
    public string CurrencyUnitTo { get; init; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }

    internal AppSettings Normalize()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            return new AppSettings();
        }

        return this with
        {
            ConverterUnitDisplayMode = Enum.IsDefined(ConverterUnitDisplayMode)
                ? ConverterUnitDisplayMode
                : ConverterUnitDisplayMode.Automatic,
            UnitConverterPreferences = UnitConverterPreferences ?? string.Empty,
            CurrencyUnitFrom = CurrencyUnitFrom ?? string.Empty,
            CurrencyUnitTo = CurrencyUnitTo ?? string.Empty
        };
    }
}

public interface ISettingsStore
{
    AppSettings Current { get; }

    event EventHandler? Changed;

    void Update(Func<AppSettings, AppSettings> update);
}

public static class AppSettingsSerializer
{
    public static string Serialize(AppSettings settings) =>
        JsonSerializer.Serialize(settings.Normalize(), AppSettingsJsonContext.Default.AppSettings);

    public static AppSettings Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppSettings();
        }

        return (JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings)
                ?? new AppSettings()).Normalize();
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
public sealed partial class AppSettingsJsonContext : JsonSerializerContext;
