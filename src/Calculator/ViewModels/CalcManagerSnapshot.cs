// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Immutable;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class CalcManagerSnapshot
    {
        public ImmutableArray<CalcManagerHistoryItem> HistoryItems { get; set; } = []; // optional
        // CalcManagerSnapshot();
        //
        // internal :;
        // explicit CalcManagerSnapshot(   CalculationManager.CalculatorManager& calcMgr);
    };
} // namespace CalculatorApp.ViewModel
