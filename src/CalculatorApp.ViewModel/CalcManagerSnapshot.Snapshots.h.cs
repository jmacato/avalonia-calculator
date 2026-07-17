// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.ObjectModel;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class CalcManagerSnapshot
    {
        public Collection<CalcManagerHistoryItem>? HistoryItems { get; private set; } // optional
                                                                                      // CalcManagerSnapshot();
                                                                                      //
                                                                                      // internal :;
                                                                                      // explicit CalcManagerSnapshot(   CalculationManager.CalculatorManager& calcMgr);
    };
} // namespace CalculatorApp.ViewModel
