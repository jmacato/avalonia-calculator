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
    // Assuming UnitConversionManager.ConversionData is a class or struct defined elsewhere
    public class ExplicitUnitConversionData : CalcManager.ConversionData
    {
        public ExplicitUnitConversionData() : base()
        {
        }

        public ExplicitUnitConversionData(ViewMode categoryId, int parentUnitId, int unitId, double ratio, double offset, bool offsetFirst = false) : base(ratio, offset, offsetFirst)
        {
            CategoryId = categoryId;
            ParentUnitId = parentUnitId;
            UnitId = unitId;
        }

        public ViewMode CategoryId { get; }
        public int ParentUnitId { get; }
        public int UnitId { get; }
    }
}
