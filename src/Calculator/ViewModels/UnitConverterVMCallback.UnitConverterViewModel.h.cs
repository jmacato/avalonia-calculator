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

public sealed class UnitConverterVMCallback : IUnitConverterVMCallback
{
    private readonly UnitConverterViewModel _viewModel;
    public UnitConverterVMCallback(UnitConverterViewModel viewModel) => _viewModel = viewModel;
    public void DisplayCallback(string from, string toValue) => _viewModel.UpdateDisplay(from, toValue);
    public void SuggestedValueCallback(IList<(string, Unit)> suggestedValues) => _viewModel.UpdateSupplementaryResults(suggestedValues);
    public void MaxDigitsReached() => _viewModel.OnMaxDigitsReached();
}
