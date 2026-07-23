// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class OperandCommand : CalcManagerExpressionCommand
    {
        public bool IsNegative { get; set; }
        public bool IsDecimalPresent { get; set; }
        public bool IsSciFmt { get; set; }
    };
} // namespace CalculatorApp.ViewModel
