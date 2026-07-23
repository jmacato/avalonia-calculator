// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalculatorApp.ViewModel.Common;
using UnitConversionManager;

namespace CalculatorApp.ViewModel;

public sealed class ViewModelCurrencyCallback(UnitConverterViewModel viewModel) : IViewModelCurrencyCallback
{
    public void CurrencyDataLoadFinished(bool didLoad)
    {
        viewModel.OnCurrencyDataLoadFinished(didLoad);
    }

    public void CurrencySymbolsCallback(string fromSymbol, string toSymbol)
    {
        viewModel.OnCurrencySymbolsUpdated(fromSymbol, toSymbol);
    }

    public void CurrencyRatiosCallback(string ratioEquality, string accRatioEquality)
    {
        viewModel.OnCurrencyRatiosUpdated(ratioEquality, accRatioEquality);
    }

    public void CurrencyTimestampCallback(string timestamp, bool isWeekOldData)
    {
        viewModel.OnCurrencyTimestampUpdated(timestamp, isWeekOldData);
    }

    public void NetworkBehaviorChanged(int newBehavior)
    {
        viewModel.OnNetworkBehaviorChanged((NetworkAccessBehavior)newBehavior);
    }
}
