// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Generic;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class UnaryCommand : ICalcManagerIExprCommand
    {
        public CalculationManager.CommandType CommandType => CalculationManager.CommandType.UnaryCommand;

        // UnaryCommand();
        //
        // internal :;
        // explicit UnaryCommand(std.vector<int> cmds);
        // std.vector<int> m_cmds;
    };
} // namespace CalculatorApp.ViewModel
