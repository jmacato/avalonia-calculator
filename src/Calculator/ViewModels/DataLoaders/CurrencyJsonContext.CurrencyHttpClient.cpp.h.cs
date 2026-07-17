// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Globalization;
using System.Text.Json.Serialization;

namespace CalculatorApp.ViewModel.DataLoaders;

[JsonSerializable(typeof(List<CurrencyMetadataRecord>))]
[JsonSerializable(typeof(List<CurrencyRateRecord>))]
[JsonSerializable(typeof(CurrencyRateSnapshot))]
internal sealed partial class CurrencyJsonContext : JsonSerializerContext;
