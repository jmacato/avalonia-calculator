// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Globalization;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace CalculatorApp.ViewModel.DataLoaders;

public sealed class CurrencyRateSnapshot
{
    public DateTimeOffset FetchedAtUtc { get; set; }
    public string BaseCurrency { get; set; } = "USD";
    public ImmutableArray<CurrencyMetadataRecord> Currencies { get; set; } = [];
    public ImmutableArray<CurrencyRateRecord> Rates { get; set; } = [];
}
