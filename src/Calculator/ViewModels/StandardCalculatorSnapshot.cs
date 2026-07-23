// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Immutable;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class StandardCalculatorSnapshot
    {
        public CalcManagerSnapshot CalcManager { get; set; } = new(); // mandatory
        public PrimaryDisplaySnapshot PrimaryDisplay { get; set; } = new(); // mandatory
        public ExpressionDisplaySnapshot? ExpressionDisplay { get; set; }  // optional
        public ImmutableArray<CalcManagerExpressionCommand> DisplayCommands { get; set; } = []; // mandatory
        // StandardCalculatorSnapshot();
    };
} // namespace CalculatorApp.ViewModel
