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
    // Assuming UnitConversionManager.Unit is a class or struct defined elsewhere
    public class OrderedUnit : CalcManager.Unit
    {
        public OrderedUnit() : base()
        {
        }

        public OrderedUnit(int id, string name, string abbreviation, int order, bool isConversionSource = false, bool isConversionTarget = false, bool isWhimsical = false) : base(id, name, abbreviation, isConversionSource, isConversionTarget, isWhimsical)
        {
            Order = order;
        }

        public int Order { get; }
    }
}
