// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.ObjectModel;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class StandardCalculatorSnapshot
    {
        public CalcManagerSnapshot CalcManager { get; set; } // mandatory
        public PrimaryDisplaySnapshot PrimaryDisplay { get; set; } // mandatory
        public ExpressionDisplaySnapshot? ExpressionDisplay { get; set; } // optional
        public Collection<ICalcManagerIExprCommand> DisplayCommands { get; } // mandatory
                                                                             // StandardCalculatorSnapshot();
    };
} // namespace CalculatorApp.ViewModel
