// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Generic;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class OperandCommand : ICalcManagerIExprCommand
    {
        public CalculationManager.CommandType CommandType => CalculationManager.CommandType.OperandCommand;

        public bool IsNegative { get; set; }
        public bool IsDecimalPresent { get; set; }
        public bool IsSciFmt { get; set; }
    };
} // namespace CalculatorApp.ViewModel
