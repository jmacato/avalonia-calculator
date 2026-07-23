// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.ViewModel.DataLoaders;

public interface ICurrencyNameProvider
{
    CurrencyDisplayMetadata GetCurrency(string isoCode, string fallbackName, string fallbackSymbol);
}
