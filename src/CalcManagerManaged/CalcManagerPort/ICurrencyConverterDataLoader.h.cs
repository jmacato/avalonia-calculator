// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using CalcEngine;
using UnitConversionManager;

namespace UnitConversionManager;

public interface ICurrencyConverterDataLoader
{
    void SetViewModelCallback(IViewModelCurrencyCallback callback);
    (string, string) GetCurrencySymbols(Unit unit1, Unit unit2);
    (string, string) GetCurrencyRatioEquality(Unit unit1, Unit unit2);
    string GetCurrencyTimestamp();
    Task<bool> TryLoadDataFromCacheAsync();
    Task<bool> TryLoadDataFromWebAsync();
    Task<bool> TryLoadDataFromWebOverrideAsync();
}
