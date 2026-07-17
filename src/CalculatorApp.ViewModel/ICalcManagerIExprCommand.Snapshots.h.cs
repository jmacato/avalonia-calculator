// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Generic;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial interface ICalcManagerIExprCommand
    {
        CalculationManager.CommandType CommandType { get; }
    }
} // namespace CalculatorApp.ViewModel
