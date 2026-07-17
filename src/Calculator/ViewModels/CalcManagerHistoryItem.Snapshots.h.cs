// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Immutable;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class CalcManagerHistoryItem
    {
        public ImmutableArray<CalcManagerToken> Tokens { get; set; } = []; // mandatory
        public ImmutableArray<CalcManagerExpressionCommand> Commands { get; set; } = []; // mandatory
        public string Expression { get; set; } = string.Empty; // mandatory
        public string Result { get; set; } = string.Empty; // mandatory
        //
        // CalcManagerHistoryItem();
        //
        // internal :;
        // explicit CalcManagerHistoryItem(   CalculationManager.HISTORYITEM& item);
    };
} // namespace CalculatorApp.ViewModel
