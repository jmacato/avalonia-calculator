// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Channels;
using System.Windows.Input;
using Avalonia.Threading;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using UnitConversionManager;

namespace CalculatorApp.ViewModel;

public sealed class ViewModelCurrencyCallback : IViewModelCurrencyCallback
{
    private readonly UnitConverterViewModel _viewModel;
    public ViewModelCurrencyCallback(UnitConverterViewModel viewModel) => _viewModel = viewModel;
    public void CurrencyDataLoadFinished(bool didLoad) => _viewModel.OnCurrencyDataLoadFinished(didLoad);
    public void CurrencySymbolsCallback(string fromSymbol, string toSymbol) => _viewModel.OnCurrencySymbolsUpdated(fromSymbol, toSymbol);
    public void CurrencyRatiosCallback(string ratioEquality, string accRatioEquality) => _viewModel.OnCurrencyRatiosUpdated(ratioEquality, accRatioEquality);
    public void CurrencyTimestampCallback(string timestamp, bool isWeekOldData) => _viewModel.OnCurrencyTimestampUpdated(timestamp, isWeekOldData);
    public void NetworkBehaviorChanged(int newBehavior) => _viewModel.OnNetworkBehaviorChanged((NetworkAccessBehavior)newBehavior);
}
