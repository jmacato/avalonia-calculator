// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Generic;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class Parentheses : ICalcManagerIExprCommand
    {
        public CalculationManager.CommandType CommandType => CalculationManager.CommandType.Parentheses;

        public int Command { get; set; }
        // Parentheses();
        //
        // internal :;
        // explicit Parentheses(int cmd);
    };
} // namespace CalculatorApp.ViewModel
