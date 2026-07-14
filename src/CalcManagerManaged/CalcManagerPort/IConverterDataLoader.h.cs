// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using CalcEngine;
using UnitConversionManager;

namespace UnitConversionManager;

public interface IConverterDataLoader
{
    void LoadData();
    IList<Category> GetOrderedCategories();
    IList<Unit> GetOrderedUnits(Category c);
    Dictionary<Unit, ConversionData> LoadOrderedRatios(Unit u);
    bool SupportsCategory(Category target);
}
