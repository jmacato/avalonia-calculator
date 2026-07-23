// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Assuming these namespaces exist in your C# project
using CalcManager = UnitConversionManager; // Alias for clarity if needed, or use full name
// For ViewMode, NavCategory etc.
// For LINQ methods like Any()

// For Debug.Assert

namespace CalculatorApp.ViewModel.Common // Adjusted namespace slightly for C# convention
{
    // Assuming UnitConversionManager.Unit is a class or struct defined elsewhere
    internal sealed class OrderedUnit : CalcManager.Unit
    {
        public OrderedUnit() : base()
        {
        }

        public OrderedUnit(UnitConverterUnits id, string name, string abbreviation, int order, bool isConversionSource = false, bool isConversionTarget = false, bool isWhimsical = false) : base((int)id, name, abbreviation, isConversionSource, isConversionTarget, isWhimsical)
        {
            Order = order;
        }

        internal int Order { get; }
    }
}
