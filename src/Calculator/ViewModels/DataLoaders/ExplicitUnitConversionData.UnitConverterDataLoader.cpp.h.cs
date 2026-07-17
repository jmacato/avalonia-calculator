// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
// Assuming these namespaces exist in your C# project
using CalcManager = UnitConversionManager; // Alias for clarity if needed, or use full name
using CalculatorApp.ViewModel.Common; // For ViewMode, NavCategory etc.
using System.Globalization;
using System.Linq; // For LINQ methods like Any()
using System.Diagnostics; // For Debug.Assert

namespace CalculatorApp.ViewModel.Common // Adjusted namespace slightly for C# convention
{
    // Assuming UnitConversionManager.ConversionData is a class or struct defined elsewhere
    internal sealed class ExplicitUnitConversionData : CalcManager.ConversionData
    {
        public ExplicitUnitConversionData() : base()
        {
        }

        public ExplicitUnitConversionData(ViewMode categoryId, UnitConverterUnits parentUnitId, UnitConverterUnits unitId, string ratio, string offset, bool offsetFirst = false) : base(ratio, offset, offsetFirst)
        {
            CategoryId = categoryId;
            ParentUnitId = parentUnitId;
            UnitId = unitId;
        }

        internal ViewMode CategoryId { get; }

        internal UnitConverterUnits ParentUnitId { get; }

        internal UnitConverterUnits UnitId { get; }
    }
}
