// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.ObjectModel;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class ExpressionDisplaySnapshot
    {
        public Collection<CalcManagerToken> Tokens { get; }
        public Collection<ICalcManagerIExprCommand> Commands { get; }
        // ExpressionDisplaySnapshot();
        //
        // internal :;
        // using CalcHistoryToken = std.pair<std.wstring, int>;
        // explicit ExpressionDisplaySnapshot(   std.vector<CalcHistoryToken>& tokens,    std.vector<std.shared_ptr<IExpressionCommand>>& commands);
    };
} // namespace CalculatorApp.ViewModel
