// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using UnitConversionManager;

namespace CalculatorApp.ViewModel;

public sealed class UnitConverterVMCallback(UnitConverterViewModel viewModel) : IUnitConverterVMCallback
{
    public void DisplayCallback(string from, string toValue)
    {
        viewModel.UpdateDisplay(from, toValue);
    }

    public void SuggestedValueCallback(IList<(string, Unit)> suggestedValues)
    {
        viewModel.UpdateSupplementaryResults(suggestedValues);
    }

    public void MaxDigitsReached()
    {
        viewModel.OnMaxDigitsReached();
    }
}
