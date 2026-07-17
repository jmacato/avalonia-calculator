// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Generic;

namespace CalculatorApp.ViewModel.Snapshot
{
    public partial class ApplicationSnapshot
    {
        public int Mode { get; set; }
        public StandardCalculatorSnapshot StandardCalculator { get; set; } = new();
    };
} // namespace CalculatorApp.ViewModel
