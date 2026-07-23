// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Assuming these namespaces exist in your C# project
// Alias for clarity if needed, or use full name
// For ViewMode, NavCategory etc.
// For LINQ methods like Any()

// For Debug.Assert

namespace CalculatorApp.ViewModel.Common // Adjusted namespace slightly for C# convention
{
    internal readonly record struct UnitData(ViewMode CategoryId, UnitConverterUnits UnitId, string Factor);
}
