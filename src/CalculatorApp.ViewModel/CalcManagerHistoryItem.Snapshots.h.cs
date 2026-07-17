// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.ObjectModel;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class CalcManagerHistoryItem
    {
        public Collection<CalcManagerToken> Tokens { get; } // mandatory
        public Collection<ICalcManagerIExprCommand> Commands { get; } // mandatory
        public string Expression { get; set; } // mandatory
        public string Result { get; set; } // mandatory
                                           //
                                           // CalcManagerHistoryItem();
                                           //
                                           // internal :;
                                           // explicit CalcManagerHistoryItem(   CalculationManager.HISTORYITEM& item);
    };
} // namespace CalculatorApp.ViewModel
