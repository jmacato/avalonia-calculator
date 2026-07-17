// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Globalization;
using CalcEngine;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using UCM = UnitConversionManager;

namespace CalculatorApp.ViewModel.DataLoaders;

internal sealed record CurrencyDataLoaderCurrencyDataSnapshot(IList<UCM.Unit> Units, Dictionary<UCM.Unit, Dictionary<UCM.Unit, UCM.ConversionData>> Ratios, Dictionary<UCM.Unit, CurrencyUnitMetadata> Metadata)
{
    public static CurrencyDataLoaderCurrencyDataSnapshot Empty { get; } = new(Array.Empty<UCM.Unit>(), [], []);
}
