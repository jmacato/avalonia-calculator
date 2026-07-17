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

internal interface IViewModelCurrencyCallback
{
    void CurrencyDataLoadFinished(bool didLoad);
    void CurrencySymbolsCallback(string fromSymbol, string toSymbol);
    void CurrencyRatiosCallback(string ratioEquality, string accRatioEquality);
    void CurrencyTimestampCallback(string timestamp, bool isWeekOldData);
    void NetworkBehaviorChanged(int newBehavior);
}
