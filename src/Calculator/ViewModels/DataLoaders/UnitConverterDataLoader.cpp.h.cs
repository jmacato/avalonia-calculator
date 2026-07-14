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
    // Assuming UnitConversionManager.Unit is a class or struct defined elsewhere
    public class OrderedUnit : CalcManager.Unit
    {
        public OrderedUnit() : base()
        {
        }

        public OrderedUnit(
            UnitConverterUnits id,
            string name,
            string abbreviation,
            int order,
            bool isConversionSource = false,
            bool isConversionTarget = false,
            bool isWhimsical = false)
            : base((int)id, name, abbreviation, isConversionSource, isConversionTarget, isWhimsical)
        {
            this.order = order;
        }



        public int order;
    }

    public struct UnitData
    {
        public ViewMode categoryId; // Assuming ViewMode is an enum or similar type
        public UnitConverterUnits unitId;
        public double factor;
    }

    // Assuming UnitConversionManager.ConversionData is a class or struct defined elsewhere
    public class ExplicitUnitConversionData : CalcManager.ConversionData
    {
        public ExplicitUnitConversionData() : base()
        {
        }

        public ExplicitUnitConversionData(
            ViewMode categoryId,
            UnitConverterUnits parentUnitId,
            UnitConverterUnits unitId,
            double ratio,
            double offset,
            bool offsetFirst = false)
            : base(ratio, offset, offsetFirst)
        {
            this.categoryId = categoryId;
            this.parentUnitId = parentUnitId;
            this.unitId = unitId;
        }

        public ViewMode categoryId;
        public UnitConverterUnits parentUnitId;
        public UnitConverterUnits unitId;
    }

    // Using partial class to split definition across files
    public partial class UnitConverterDataLoader : CalcManager.IConverterDataLoader // No C# equivalent for enable_shared_from_this needed
    {
        // Member variable declarations (can be here or in the other partial file)
        private readonly List<CalcManager.Category> m_categoryList;
        private readonly Dictionary<int, List<CalcManager.Unit>> m_categoryIDToUnitsMap;
        // Assuming CalcManager.Unit correctly implements Equals and GetHashCode for Dictionary key usage
        private readonly Dictionary<CalcManager.Unit, Dictionary<CalcManager.Unit, CalcManager.ConversionData>> m_ratioMap;
        private readonly string m_currentRegionCode;

        // Constructor
        public UnitConverterDataLoader(string? regionCode = null)
        {
            m_currentRegionCode = regionCode ?? GetCurrentRegionCode();
            m_categoryList = new List<CalcManager.Category>();
            m_categoryIDToUnitsMap = new Dictionary<int, List<CalcManager.Unit>>();
            m_ratioMap = new Dictionary<CalcManager.Unit, Dictionary<CalcManager.Unit, CalcManager.ConversionData>>();
        }

        private static string GetCurrentRegionCode()
        {
            try
            {
                return RegionInfo.CurrentRegion.TwoLetterISORegionName;
            }
            catch (ArgumentException)
            {
                return "US";
            }
        }

    }
}
