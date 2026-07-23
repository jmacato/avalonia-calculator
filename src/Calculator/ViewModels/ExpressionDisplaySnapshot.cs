// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Immutable;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class ExpressionDisplaySnapshot
    {
        public ImmutableArray<CalcManagerToken> Tokens { get; set; } = [];
        public ImmutableArray<CalcManagerExpressionCommand> Commands { get; set; } = [];
        // ExpressionDisplaySnapshot();
        //
        // internal :;
        // using CalcHistoryToken = std.pair<std.wstring, int>;
        // explicit ExpressionDisplaySnapshot(   std.vector<CalcHistoryToken>& tokens,    std.vector<std.shared_ptr<IExpressionCommand>>& commands);
    };
} // namespace CalculatorApp.ViewModel
