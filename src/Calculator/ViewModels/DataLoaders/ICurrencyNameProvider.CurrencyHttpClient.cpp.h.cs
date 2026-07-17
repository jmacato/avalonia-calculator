// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Globalization;
using System.Text.Json.Serialization;

namespace CalculatorApp.ViewModel.DataLoaders;

public interface ICurrencyNameProvider
{
    CurrencyDisplayMetadata GetCurrency(string isoCode, string fallbackName, string fallbackSymbol);
}
