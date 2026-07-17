// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CalculatorApp.Services.Settings;

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public ConverterUnitDisplayMode ConverterUnitDisplayMode { get; init; } = ConverterUnitDisplayMode.Automatic;
    public bool AutomaticCurrencyRefresh { get; init; } = true;
    public bool GraphThemeMatchApp { get; init; }
    public string UnitConverterPreferences { get; init; } = string.Empty;

    [JsonPropertyName("lastCurrencyFrom")]
    public string CurrencyUnitFrom { get; init; } = string.Empty;

    [JsonPropertyName("lastCurrencyTo")]
    public string CurrencyUnitTo { get; init; } = string.Empty;

    [JsonInclude]
    [JsonExtensionData]
    internal Dictionary<string, JsonElement> AdditionalDataStorage { get; set; } = [];

    [JsonIgnore]
    public IReadOnlyDictionary<string, JsonElement> AdditionalData => AdditionalDataStorage;

    internal AppSettings Normalize()
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            return new AppSettings();
        }

        return this with
        {
            ConverterUnitDisplayMode = Enum.IsDefined(ConverterUnitDisplayMode) ? ConverterUnitDisplayMode : ConverterUnitDisplayMode.Automatic,
            UnitConverterPreferences = UnitConverterPreferences ?? string.Empty,
            CurrencyUnitFrom = CurrencyUnitFrom ?? string.Empty,
            CurrencyUnitTo = CurrencyUnitTo ?? string.Empty
        };
    }
}
