// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CategorySelectionInitializer = (System.Collections.Generic.List<UnitConversionManager.Unit>, UnitConversionManager.Unit, UnitConversionManager.Unit);
using CategoryToUnitVectorMap = System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<UnitConversionManager.Unit>>;
using WString = string;
using wstring_view = string;

namespace UnitConversionManager;

internal interface ICurrencyConverterDataLoader
{
    void SetViewModelCallback(IViewModelCurrencyCallback callback);
    (string, string) GetCurrencySymbols(Unit unit1, Unit unit2);
    (string, string) GetCurrencyRatioEquality(Unit unit1, Unit unit2);
    string GetCurrencyTimestamp();
    Task<bool> TryLoadDataFromCacheAsync();
    Task<bool> TryLoadDataFromWebAsync();
    Task<bool> TryLoadDataFromWebOverrideAsync();
}
