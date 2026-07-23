// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// Assuming these namespaces exist in your C# project
using CalcManager = UnitConversionManager; // Alias for clarity if needed, or use full name
// For ViewMode, NavCategory etc.
using System.Globalization;

namespace CalculatorApp.ViewModel.Common // Adjusted namespace slightly for C# convention
{
    // Using partial class to split definition across files
    public partial class UnitConverterDataLoader(string? regionCode = null)
        : CalcManager.IConverterDataLoader // No C# equivalent for enable_shared_from_this needed
    {
        // Member variable declarations (can be here or in the other partial file)
        private readonly List<CalcManager.Category> m_categoryList = [];
        private readonly Dictionary<int, List<CalcManager.Unit>> m_categoryIDToUnitsMap = [];
        // Assuming CalcManager.Unit correctly implements Equals and GetHashCode for Dictionary key usage
        private readonly Dictionary<CalcManager.Unit, Dictionary<CalcManager.Unit, CalcManager.ConversionData>> m_ratioMap = [];
        private readonly Dictionary<int, OrderedUnit> m_idToUnit = [];
        private readonly Dictionary<int, ViewMode> m_unitIDToCategoryMap = [];
        private readonly string m_currentRegionCode = regionCode ?? GetCurrentRegionCode();
        private Dictionary<ViewMode, Dictionary<int, string>> m_categoryToUnitConversionDataMap = [];
        private Dictionary<int, Dictionary<int, CalcManager.ConversionData>> m_explicitConversionData = [];
        private UnitConverterLocalizedData? m_localizedData;
        // Constructor

        /// <summary>
        /// Resolves localized category and unit names on the owning UI thread.
        /// The resulting object graph is then transferred to the converter worker
        /// through its bounded request channel.
        /// </summary>
        internal void PrepareLocalizedData()
        {
            if (Volatile.Read(ref m_localizedData) is not null)
            {
                return;
            }

            var localizedData = new UnitConverterLocalizedData(
                CreateCategories(),
                GetUnits());
            Volatile.Write(ref m_localizedData, localizedData);
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
