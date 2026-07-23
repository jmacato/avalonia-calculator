// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.ViewModel.DataLoaders;

public enum CurrencyLoadStatus
{
    NotLoaded,
    FailedToLoad,
    LoadedFromCache,
    LoadedFromWeb
}
