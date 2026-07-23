// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace CalculatorApp.ViewModel.DataLoaders;

public sealed class CurrencyRateRecord
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("base")]
    public string BaseCurrency { get; set; } = string.Empty;

    [JsonPropertyName("quote")]
    public string QuoteCurrency { get; set; } = string.Empty;

    [JsonPropertyName("rate")]
    public decimal Rate { get; set; }
}
