// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.ViewModel.DataLoaders;

public interface ICurrencyRateProvider
{
    Task<CurrencyRateSnapshot> GetLatestRatesAsync(CancellationToken cancellationToken = default);
}
