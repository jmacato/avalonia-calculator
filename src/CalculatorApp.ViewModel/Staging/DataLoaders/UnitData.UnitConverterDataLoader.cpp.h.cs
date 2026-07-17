// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
// Assuming these namespaces exist in your C# project
using CalcManager = UnitConversionManager; // Alias for clarity if needed, or use full name
using CalculatorApp.ViewModel.Common; // For ViewMode, NavCategory etc.
using Windows.Globalization;
using System.Linq; // For LINQ methods like Any()
using System.Diagnostics; // For Debug.Assert

namespace CalculatorApp.ViewModel.Common // Adjusted namespace slightly for C# convention
{
    public record struct UnitData
    {
        public ViewMode CategoryId { get; set; }
        public int UnitId { get; set; }
        public double Factor { get; set; }
    }
}
