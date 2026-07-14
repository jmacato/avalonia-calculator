// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using CalcEngine;
using UnitConversionManager;

namespace UnitConversionManager;

public interface IViewModelCurrencyCallback
{
    void CurrencyDataLoadFinished(bool didLoad);
    void CurrencySymbolsCallback(string fromSymbol, string toSymbol);
    void CurrencyRatiosCallback(string ratioEquality, string accRatioEquality);
    void CurrencyTimestampCallback(string timestamp, bool isWeekOldData);
    void NetworkBehaviorChanged(int newBehavior);
}
