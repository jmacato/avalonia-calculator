// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

using CalcManager = UnitConversionManager;

namespace CalculatorApp.ViewModel.Common
{
    public partial class UnitConverterDataLoader // Continues the class definition
    {
        private const bool CONVERT_WITH_OFFSET_FIRST = true;

        // Interface Method Implementations

        public IList<CalcManager.Category> GetOrderedCategories()
        {
            return m_categoryList;
        }

        public IList<CalcManager.Unit> GetOrderedUnits(CalcManager.Category category)
        {
            return m_categoryIDToUnitsMap[category.Id];
        }

        public Dictionary<CalcManager.Unit, CalcManager.ConversionData> LoadOrderedRatios(CalcManager.Unit unit)
        {
            return m_ratioMap[unit];
        }

        public bool SupportsCategory(CalcManager.Category target)
        {
            if (m_categoryList.Count == 0)
            {
                GetCategories();
            }

            int currencyId = NavCategoryStates.Serialize(ViewMode.Currency);

            return m_categoryList.Any(category => currencyId != category.Id && target.Id == category.Id);
        }

        public void LoadData()
        {
            var idToUnit = new Dictionary<int, OrderedUnit>();

            // Load categories, units and conversion data into data structures.
            GetCategories();
            Dictionary<ViewMode, List<OrderedUnit>> orderedUnitMap = GetUnits();
            Dictionary<ViewMode, Dictionary<int, double>> categoryToUnitConversionDataMap = GetConversionData();
            Dictionary<int, Dictionary<int, CalcManager.ConversionData>> explicitConversionData = GetExplicitConversionData(); // This is needed for temperature conversions

            m_categoryIDToUnitsMap.Clear();
            m_ratioMap.Clear();

            foreach (CalcManager.Category objectCategory in m_categoryList)
            {
                ViewMode categoryViewMode = NavCategoryStates.Deserialize(objectCategory.Id);

                Debug.Assert(NavCategory.IsConverterViewMode(categoryViewMode));

                if (categoryViewMode == ViewMode.Currency)
                {
                    // Currency is an ordered category but we do not want to process it here
                    // because this function is not thread-safe and currency data is asynchronously
                    // loaded.
                    m_categoryIDToUnitsMap.Add(objectCategory.Id, new List<CalcManager.Unit>());
                    continue;
                }

                if (!orderedUnitMap.TryGetValue(categoryViewMode, out List<OrderedUnit>? orderedUnits))
                {
                    Debug.WriteLine($"Warning: No units found for category {categoryViewMode}");
                    orderedUnits = new List<OrderedUnit>();
                }

                var unitList = new List<CalcManager.Unit>();

                // Sort the units by order
                orderedUnits.Sort((first, second) => first.order.CompareTo(second.order));

                foreach (OrderedUnit u in orderedUnits)
                {
                    unitList.Add(u);
                    idToUnit.Add(u.Id, u);
                }

                // Save units per category
                m_categoryIDToUnitsMap.Add(objectCategory.Id, unitList);

                // For each unit, populate the conversion data
                foreach (CalcManager.Unit unit in unitList)
                {
                    var conversions = new Dictionary<CalcManager.Unit, CalcManager.ConversionData>();

                    if (!explicitConversionData.ContainsKey(unit.Id))
                    {
                        // Get the associated units for a category id
                        if (!categoryToUnitConversionDataMap.TryGetValue(categoryViewMode, out Dictionary<int, double>? unitConversions))
                        {
                            Debug.WriteLine($"Warning: No conversion data found for category {categoryViewMode}");
                            unitConversions = new Dictionary<int, double>();
                        }

                        if (!unitConversions.TryGetValue(unit.Id, out double unitFactor))
                        {
                             Debug.WriteLine($"Warning: Unit factor not found for unit {unit.Id} in category {categoryViewMode}");
                             continue;
                        }

                        foreach (var kvp in unitConversions)
                        {
                            int id = kvp.Key;
                            double conversionFactor = kvp.Value;

                            if (!idToUnit.ContainsKey(id))
                            {
                                // Optional units will not be in idToUnit but can be in unitConversions.
                                // For optional units that did not make it to the current set of units, just continue.
                                continue;
                            }

                            var parsedData = new CalcManager.ConversionData { Ratio = 1.0, Offset = 0.0, OffsetFirst = false };
                            Debug.Assert(conversionFactor > 0); // divide by zero assert
                            parsedData.Ratio = unitFactor / conversionFactor;
                            conversions.Add(idToUnit[id], parsedData);
                        }
                    }
                    else
                    {
                        Dictionary<int, CalcManager.ConversionData> unitConversions = explicitConversionData[unit.Id];
                        foreach (var kvp in unitConversions)
                        {
                             if (idToUnit.TryGetValue(kvp.Key, out OrderedUnit? targetUnit))
                             {
                                conversions.Add(targetUnit, kvp.Value);
                             }
                             else
                             {
                                Debug.WriteLine($"Warning: Target unit {kvp.Key} not found in idToUnit map for explicit conversion from {unit.Id}");
                             }
                        }
                    }

                    m_ratioMap.Add(unit, conversions);
                }
            }
        }

        // Helper Method Implementations

        private void GetCategories()
        {
            m_categoryList.Clear();
            var converterCategoryGroup = NavCategoryStates.CreateConverterCategoryGroup();
            foreach (var category in converterCategoryGroup.Categories)
            {
                /* Id, CategoryName, SupportsNegative */
                m_categoryList.Add(new CalcManager.Category(
                    NavCategoryStates.Serialize(category.ViewMode),
                    category.Name,
                    category.SupportsNegative));
            }
        }

        private Dictionary<ViewMode, List<OrderedUnit>> GetUnits()
        {
            var unitMap = new Dictionary<ViewMode, List<OrderedUnit>>();

            // US + Federated States of Micronesia, Marshall Islands, Palau
            bool useUSCustomaryAndFahrenheit =
                m_currentRegionCode == "US" || m_currentRegionCode == "FM" || m_currentRegionCode == "MH" || m_currentRegionCode == "PW";

            // useUSCustomaryAndFahrenheit + Liberia
            // Source: https://en.wikipedia.org/wiki/Metrication
            bool useUSCustomary = useUSCustomaryAndFahrenheit || m_currentRegionCode == "LR";

            // Use 'Syst�me International' (International System of Units - Metrics)
            bool useSI = !useUSCustomary;

            // useUSCustomaryAndFahrenheit + the Bahamas, the Cayman Islands and Liberia
            // Source: http://en.wikipedia.org/wiki/Fahrenheit
            bool useFahrenheit = useUSCustomaryAndFahrenheit || m_currentRegionCode == "BS" || m_currentRegionCode == "KY" || m_currentRegionCode == "LR";

            bool useWattInsteadOfKilowatt = m_currentRegionCode == "GB";

            // Use Pyeong, a Korean floorspace unit.
            // https://en.wikipedia.org/wiki/Korean_units_of_measurement#Area
            bool usePyeong = m_currentRegionCode == "KP" || m_currentRegionCode == "KR";


            // --- Area Units ---
            var areaUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.Area_Acre, GetLocalizedStringName("UnitName_Acre"), GetLocalizedStringName("UnitAbbreviation_Acre"), 9 ),
                new OrderedUnit( UnitConverterUnits.Area_Hectare, GetLocalizedStringName("UnitName_Hectare"), GetLocalizedStringName("UnitAbbreviation_Hectare"), 4 ),
                new OrderedUnit( UnitConverterUnits.Area_SquareCentimeter, GetLocalizedStringName("UnitName_SquareCentimeter"), GetLocalizedStringName("UnitAbbreviation_SquareCentimeter"), 2 ),
                new OrderedUnit( UnitConverterUnits.Area_SquareFoot, GetLocalizedStringName("UnitName_SquareFoot"), GetLocalizedStringName("UnitAbbreviation_SquareFoot"), 7, useSI, useUSCustomary, false ),
                new OrderedUnit( UnitConverterUnits.Area_SquareInch, GetLocalizedStringName("UnitName_SquareInch"), GetLocalizedStringName("UnitAbbreviation_SquareInch"), 6 ),
                new OrderedUnit( UnitConverterUnits.Area_SquareKilometer, GetLocalizedStringName("UnitName_SquareKilometer"), GetLocalizedStringName("UnitAbbreviation_SquareKilometer"), 5 ),
                new OrderedUnit( UnitConverterUnits.Area_SquareMeter, GetLocalizedStringName("UnitName_SquareMeter"), GetLocalizedStringName("UnitAbbreviation_SquareMeter"), 3, useUSCustomary, useSI, false ),
                new OrderedUnit( UnitConverterUnits.Area_SquareMile, GetLocalizedStringName("UnitName_SquareMile"), GetLocalizedStringName("UnitAbbreviation_SquareMile"), 10 ),
                new OrderedUnit( UnitConverterUnits.Area_SquareMillimeter, GetLocalizedStringName("UnitName_SquareMillimeter"), GetLocalizedStringName("UnitAbbreviation_SquareMillimeter"), 1 ),
                new OrderedUnit( UnitConverterUnits.Area_SquareYard, GetLocalizedStringName("UnitName_SquareYard"), GetLocalizedStringName("UnitAbbreviation_SquareYard"), 8 ),
                new OrderedUnit( UnitConverterUnits.Area_Hand, GetLocalizedStringName("UnitName_Hand"), GetLocalizedStringName("UnitAbbreviation_Hand"), 11, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Area_Paper, GetLocalizedStringName("UnitName_Paper"), GetLocalizedStringName("UnitAbbreviation_Paper"), 12, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Area_SoccerField, GetLocalizedStringName("UnitName_SoccerField"), GetLocalizedStringName("UnitAbbreviation_SoccerField"), 13, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Area_Castle, GetLocalizedStringName("UnitName_Castle"), GetLocalizedStringName("UnitAbbreviation_Castle"), 14, false, false, true )
            };
            if (usePyeong)
            {
                areaUnits.Add(new OrderedUnit( UnitConverterUnits.Area_Pyeong, GetLocalizedStringName("UnitName_Pyeong"), GetLocalizedStringName("UnitAbbreviation_Pyeong"), 15, false, false, false ));
            }
            unitMap.Add(ViewMode.Area, areaUnits);

            // --- Data Units ---
            var dataUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.Data_Bit, GetLocalizedStringName("UnitName_Bit"), GetLocalizedStringName("UnitAbbreviation_Bit"), 1 ),
                new OrderedUnit( UnitConverterUnits.Data_Byte, GetLocalizedStringName("UnitName_Byte"), GetLocalizedStringName("UnitAbbreviation_Byte"), 3 ),
                new OrderedUnit( UnitConverterUnits.Data_Exabits, GetLocalizedStringName("UnitName_Exabits"), GetLocalizedStringName("UnitAbbreviation_Exabits"), 24 ),
                new OrderedUnit( UnitConverterUnits.Data_Exabytes, GetLocalizedStringName("UnitName_Exabytes"), GetLocalizedStringName("UnitAbbreviation_Exabytes"), 26 ),
                new OrderedUnit( UnitConverterUnits.Data_Exbibits, GetLocalizedStringName("UnitName_Exbibits"), GetLocalizedStringName("UnitAbbreviation_Exbibits"), 25 ),
                new OrderedUnit( UnitConverterUnits.Data_Exbibytes, GetLocalizedStringName("UnitName_Exbibytes"), GetLocalizedStringName("UnitAbbreviation_Exbibytes"), 27 ),
                new OrderedUnit( UnitConverterUnits.Data_Gibibits, GetLocalizedStringName("UnitName_Gibibits"), GetLocalizedStringName("UnitAbbreviation_Gibibits"), 13 ),
                new OrderedUnit( UnitConverterUnits.Data_Gibibytes, GetLocalizedStringName("UnitName_Gibibytes"), GetLocalizedStringName("UnitAbbreviation_Gibibytes"), 15 ),
                new OrderedUnit( UnitConverterUnits.Data_Gigabit, GetLocalizedStringName("UnitName_Gigabit"), GetLocalizedStringName("UnitAbbreviation_Gigabit"), 12 ),
                new OrderedUnit( UnitConverterUnits.Data_Gigabyte, GetLocalizedStringName("UnitName_Gigabyte"), GetLocalizedStringName("UnitAbbreviation_Gigabyte"), 14, true, false, false ),
                new OrderedUnit( UnitConverterUnits.Data_Kibibits, GetLocalizedStringName("UnitName_Kibibits"), GetLocalizedStringName("UnitAbbreviation_Kibibits"), 5 ),
                new OrderedUnit( UnitConverterUnits.Data_Kibibytes, GetLocalizedStringName("UnitName_Kibibytes"), GetLocalizedStringName("UnitAbbreviation_Kibibytes"), 7 ),
                new OrderedUnit( UnitConverterUnits.Data_Kilobit, GetLocalizedStringName("UnitName_Kilobit"), GetLocalizedStringName("UnitAbbreviation_Kilobit"), 4 ),
                new OrderedUnit( UnitConverterUnits.Data_Kilobyte, GetLocalizedStringName("UnitName_Kilobyte"), GetLocalizedStringName("UnitAbbreviation_Kilobyte"), 6 ),
                new OrderedUnit( UnitConverterUnits.Data_Mebibits, GetLocalizedStringName("UnitName_Mebibits"), GetLocalizedStringName("UnitAbbreviation_Mebibits"), 9 ),
                new OrderedUnit( UnitConverterUnits.Data_Mebibytes, GetLocalizedStringName("UnitName_Mebibytes"), GetLocalizedStringName("UnitAbbreviation_Mebibytes"), 11 ),
                new OrderedUnit( UnitConverterUnits.Data_Megabit, GetLocalizedStringName("UnitName_Megabit"), GetLocalizedStringName("UnitAbbreviation_Megabit"), 8 ),
                new OrderedUnit( UnitConverterUnits.Data_Megabyte, GetLocalizedStringName("UnitName_Megabyte"), GetLocalizedStringName("UnitAbbreviation_Megabyte"), 10, false, true, false ),
                new OrderedUnit( UnitConverterUnits.Data_Nibble, GetLocalizedStringName("UnitName_Nibble"), GetLocalizedStringName("UnitAbbreviation_Nibble"), 2 ),
                new OrderedUnit( UnitConverterUnits.Data_Pebibits, GetLocalizedStringName("UnitName_Pebibits"), GetLocalizedStringName("UnitAbbreviation_Pebibits"), 21 ),
                new OrderedUnit( UnitConverterUnits.Data_Pebibytes, GetLocalizedStringName("UnitName_Pebibytes"), GetLocalizedStringName("UnitAbbreviation_Pebibytes"), 23 ),
                new OrderedUnit( UnitConverterUnits.Data_Petabit, GetLocalizedStringName("UnitName_Petabit"), GetLocalizedStringName("UnitAbbreviation_Petabit"), 20 ),
                new OrderedUnit( UnitConverterUnits.Data_Petabyte, GetLocalizedStringName("UnitName_Petabyte"), GetLocalizedStringName("UnitAbbreviation_Petabyte"), 22 ),
                new OrderedUnit( UnitConverterUnits.Data_Tebibits, GetLocalizedStringName("UnitName_Tebibits"), GetLocalizedStringName("UnitAbbreviation_Tebibits"), 17 ),
                new OrderedUnit( UnitConverterUnits.Data_Tebibytes, GetLocalizedStringName("UnitName_Tebibytes"), GetLocalizedStringName("UnitAbbreviation_Tebibytes"), 19 ),
                new OrderedUnit( UnitConverterUnits.Data_Terabit, GetLocalizedStringName("UnitName_Terabit"), GetLocalizedStringName("UnitAbbreviation_Terabit"), 16 ),
                new OrderedUnit( UnitConverterUnits.Data_Terabyte, GetLocalizedStringName("UnitName_Terabyte"), GetLocalizedStringName("UnitAbbreviation_Terabyte"), 18 ),
                new OrderedUnit( UnitConverterUnits.Data_Yobibits, GetLocalizedStringName("UnitName_Yobibits"), GetLocalizedStringName("UnitAbbreviation_Yobibits"), 33 ),
                new OrderedUnit( UnitConverterUnits.Data_Yobibytes, GetLocalizedStringName("UnitName_Yobibytes"), GetLocalizedStringName("UnitAbbreviation_Yobibytes"), 35 ),
                new OrderedUnit( UnitConverterUnits.Data_Yottabit, GetLocalizedStringName("UnitName_Yottabit"), GetLocalizedStringName("UnitAbbreviation_Yottabit"), 32 ),
                new OrderedUnit( UnitConverterUnits.Data_Yottabyte, GetLocalizedStringName("UnitName_Yottabyte"), GetLocalizedStringName("UnitAbbreviation_Yottabyte"), 34 ),
                new OrderedUnit( UnitConverterUnits.Data_Zebibits, GetLocalizedStringName("UnitName_Zebibits"), GetLocalizedStringName("UnitAbbreviation_Zebibits"), 29 ),
                new OrderedUnit( UnitConverterUnits.Data_Zebibytes, GetLocalizedStringName("UnitName_Zebibytes"), GetLocalizedStringName("UnitAbbreviation_Zebibytes"), 31 ),
                new OrderedUnit( UnitConverterUnits.Data_Zetabits, GetLocalizedStringName("UnitName_Zetabits"), GetLocalizedStringName("UnitAbbreviation_Zetabits"), 28 ),
                new OrderedUnit( UnitConverterUnits.Data_Zetabytes, GetLocalizedStringName("UnitName_Zetabytes"), GetLocalizedStringName("UnitAbbreviation_Zetabytes"), 30 ),
                new OrderedUnit( UnitConverterUnits.Data_FloppyDisk, GetLocalizedStringName("UnitName_FloppyDisk"), GetLocalizedStringName("UnitAbbreviation_FloppyDisk"), 13, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Data_CD, GetLocalizedStringName("UnitName_CD"), GetLocalizedStringName("UnitAbbreviation_CD"), 14, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Data_DVD, GetLocalizedStringName("UnitName_DVD"), GetLocalizedStringName("UnitAbbreviation_DVD"), 15, false, false, true )
            };
            unitMap.Add(ViewMode.Data, dataUnits);

            // --- Energy Units ---
            var energyUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.Energy_BritishThermalUnit, GetLocalizedStringName("UnitName_BritishThermalUnit"), GetLocalizedStringName("UnitAbbreviation_BritishThermalUnit"), 7 ),
                new OrderedUnit( UnitConverterUnits.Energy_Calorie, GetLocalizedStringName("UnitName_Calorie"), GetLocalizedStringName("UnitAbbreviation_Calorie"), 4 ),
                new OrderedUnit( UnitConverterUnits.Energy_ElectronVolt, GetLocalizedStringName("UnitName_Electron-Volt"), GetLocalizedStringName("UnitAbbreviation_Electron-Volt"), 1 ),
                new OrderedUnit( UnitConverterUnits.Energy_FootPound, GetLocalizedStringName("UnitName_Foot-Pound"), GetLocalizedStringName("UnitAbbreviation_Foot-Pound"), 6 ),
                new OrderedUnit( UnitConverterUnits.Energy_Joule, GetLocalizedStringName("UnitName_Joule"), GetLocalizedStringName("UnitAbbreviation_Joule"), 2, true, false, false ),
                new OrderedUnit( UnitConverterUnits.Energy_Kilowatthour, GetLocalizedStringName("UnitName_Kilowatthour"), GetLocalizedStringName("UnitAbbreviation_Kilowatthour"), 166, true, false, false ),
                new OrderedUnit( UnitConverterUnits.Energy_Kilocalorie, GetLocalizedStringName("UnitName_Kilocalorie"), GetLocalizedStringName("UnitAbbreviation_Kilocalorie"), 5, false, true, false ),
                new OrderedUnit( UnitConverterUnits.Energy_Kilojoule, GetLocalizedStringName("UnitName_Kilojoule"), GetLocalizedStringName("UnitAbbreviation_Kilojoule"), 3 ),
                new OrderedUnit( UnitConverterUnits.Energy_Battery, GetLocalizedStringName("UnitName_Battery"), GetLocalizedStringName("UnitAbbreviation_Battery"), 8, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Energy_Banana, GetLocalizedStringName("UnitName_Banana"), GetLocalizedStringName("UnitAbbreviation_Banana"), 9, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Energy_SliceOfCake, GetLocalizedStringName("UnitName_SliceOfCake"), GetLocalizedStringName("UnitAbbreviation_SliceOfCake"), 10, false, false, true )
            };
            unitMap.Add(ViewMode.Energy, energyUnits);

            // --- Length Units ---
            var lengthUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.Length_Angstrom, GetLocalizedStringName("UnitName_Angstrom"), GetLocalizedStringName("UnitAbbreviation_Angstrom"), 1 ),
                new OrderedUnit( UnitConverterUnits.Length_Centimeter, GetLocalizedStringName("UnitName_Centimeter"), GetLocalizedStringName("UnitAbbreviation_Centimeter"), 5, useUSCustomary, useSI, false ),
                new OrderedUnit( UnitConverterUnits.Length_Foot, GetLocalizedStringName("UnitName_Foot"), GetLocalizedStringName("UnitAbbreviation_Foot"), 9 ),
                new OrderedUnit( UnitConverterUnits.Length_Inch, GetLocalizedStringName("UnitName_Inch"), GetLocalizedStringName("UnitAbbreviation_Inch"), 8, useSI, useUSCustomary, false ),
                new OrderedUnit( UnitConverterUnits.Length_Kilometer, GetLocalizedStringName("UnitName_Kilometer"), GetLocalizedStringName("UnitAbbreviation_Kilometer"), 7 ),
                new OrderedUnit( UnitConverterUnits.Length_Meter, GetLocalizedStringName("UnitName_Meter"), GetLocalizedStringName("UnitAbbreviation_Meter"), 6 ),
                new OrderedUnit( UnitConverterUnits.Length_Micron, GetLocalizedStringName("UnitName_Micron"), GetLocalizedStringName("UnitAbbreviation_Micron"), 3 ),
                new OrderedUnit( UnitConverterUnits.Length_Mile, GetLocalizedStringName("UnitName_Mile"), GetLocalizedStringName("UnitAbbreviation_Mile"), 11 ),
                new OrderedUnit( UnitConverterUnits.Length_Millimeter, GetLocalizedStringName("UnitName_Millimeter"), GetLocalizedStringName("UnitAbbreviation_Millimeter"), 4 ),
                new OrderedUnit( UnitConverterUnits.Length_Nanometer, GetLocalizedStringName("UnitName_Nanometer"), GetLocalizedStringName("UnitAbbreviation_Nanometer"), 2 ),
                new OrderedUnit( UnitConverterUnits.Length_NauticalMile, GetLocalizedStringName("UnitName_NauticalMile"), GetLocalizedStringName("UnitAbbreviation_NauticalMile"), 12 ),
                new OrderedUnit( UnitConverterUnits.Length_Yard, GetLocalizedStringName("UnitName_Yard"), GetLocalizedStringName("UnitAbbreviation_Yard"), 10 ),
                new OrderedUnit( UnitConverterUnits.Length_Paperclip, GetLocalizedStringName("UnitName_Paperclip"), GetLocalizedStringName("UnitAbbreviation_Paperclip"), 13, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Length_Hand, GetLocalizedStringName("UnitName_Hand"), GetLocalizedStringName("UnitAbbreviation_Hand"), 14, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Length_JumboJet, GetLocalizedStringName("UnitName_JumboJet"), GetLocalizedStringName("UnitAbbreviation_JumboJet"), 15, false, false, true )
            };
            unitMap.Add(ViewMode.Length, lengthUnits);

            // --- Power Units ---
            var powerUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.Power_BritishThermalUnitPerMinute, GetLocalizedStringName("UnitName_BTUPerMinute"), GetLocalizedStringName("UnitAbbreviation_BTUPerMinute"), 5 ),
                new OrderedUnit( UnitConverterUnits.Power_FootPoundPerMinute, GetLocalizedStringName("UnitName_Foot-PoundPerMinute"), GetLocalizedStringName("UnitAbbreviation_Foot-PoundPerMinute"), 4 ),
                new OrderedUnit( UnitConverterUnits.Power_Horsepower, GetLocalizedStringName("UnitName_Horsepower"), GetLocalizedStringName("UnitAbbreviation_Horsepower"), 3, false, true, false ),
                new OrderedUnit( UnitConverterUnits.Power_Kilowatt, GetLocalizedStringName("UnitName_Kilowatt"), GetLocalizedStringName("UnitAbbreviation_Kilowatt"), 2, !useWattInsteadOfKilowatt, false, false ),
                new OrderedUnit( UnitConverterUnits.Power_Watt, GetLocalizedStringName("UnitName_Watt"), GetLocalizedStringName("UnitAbbreviation_Watt"), 1, useWattInsteadOfKilowatt, false, false ),
                new OrderedUnit( UnitConverterUnits.Power_LightBulb, GetLocalizedStringName("UnitName_LightBulb"), GetLocalizedStringName("UnitAbbreviation_LightBulb"), 6, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Power_Horse, GetLocalizedStringName("UnitName_Horse"), GetLocalizedStringName("UnitAbbreviation_Horse"), 7, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Power_TrainEngine, GetLocalizedStringName("UnitName_TrainEngine"), GetLocalizedStringName("UnitAbbreviation_TrainEngine"), 8, false, false, true )
            };
            unitMap.Add(ViewMode.Power, powerUnits);

            // --- Temperature Units ---
            var tempUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.Temperature_DegreesCelsius, GetLocalizedStringName("UnitName_DegreesCelsius"), GetLocalizedStringName("UnitAbbreviation_DegreesCelsius"), 1, useFahrenheit, !useFahrenheit, false ),
                new OrderedUnit( UnitConverterUnits.Temperature_DegreesFahrenheit, GetLocalizedStringName("UnitName_DegreesFahrenheit"), GetLocalizedStringName("UnitAbbreviation_DegreesFahrenheit"), 2, !useFahrenheit, useFahrenheit, false ),
                new OrderedUnit( UnitConverterUnits.Temperature_Kelvin, GetLocalizedStringName("UnitName_Kelvin"), GetLocalizedStringName("UnitAbbreviation_Kelvin"), 3 )
            };
            unitMap.Add(ViewMode.Temperature, tempUnits);

            // --- Time Units ---
            var timeUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.Time_Day, GetLocalizedStringName("UnitName_Day"), GetLocalizedStringName("UnitAbbreviation_Day"), 6 ),
                new OrderedUnit( UnitConverterUnits.Time_Hour, GetLocalizedStringName("UnitName_Hour"), GetLocalizedStringName("UnitAbbreviation_Hour"), 5, true, false, false ),
                new OrderedUnit( UnitConverterUnits.Time_Microsecond, GetLocalizedStringName("UnitName_Microsecond"), GetLocalizedStringName("UnitAbbreviation_Microsecond"), 1 ),
                new OrderedUnit( UnitConverterUnits.Time_Millisecond, GetLocalizedStringName("UnitName_Millisecond"), GetLocalizedStringName("UnitAbbreviation_Millisecond"), 2 ),
                new OrderedUnit( UnitConverterUnits.Time_Minute, GetLocalizedStringName("UnitName_Minute"), GetLocalizedStringName("UnitAbbreviation_Minute"), 4, false, true, false ),
                new OrderedUnit( UnitConverterUnits.Time_Second, GetLocalizedStringName("UnitName_Second"), GetLocalizedStringName("UnitAbbreviation_Second"), 3 ),
                new OrderedUnit( UnitConverterUnits.Time_Week, GetLocalizedStringName("UnitName_Week"), GetLocalizedStringName("UnitAbbreviation_Week"), 7 ),
                new OrderedUnit( UnitConverterUnits.Time_Year, GetLocalizedStringName("UnitName_Year"), GetLocalizedStringName("UnitAbbreviation_Year"), 8 )
            };
            unitMap.Add(ViewMode.Time, timeUnits);

            // --- Speed Units ---
            var speedUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.Speed_CentimetersPerSecond, GetLocalizedStringName("UnitName_CentimetersPerSecond"), GetLocalizedStringName("UnitAbbreviation_CentimetersPerSecond"), 1 ),
                new OrderedUnit( UnitConverterUnits.Speed_FeetPerSecond, GetLocalizedStringName("UnitName_FeetPerSecond"), GetLocalizedStringName("UnitAbbreviation_FeetPerSecond"), 4 ),
                new OrderedUnit( UnitConverterUnits.Speed_KilometersPerHour, GetLocalizedStringName("UnitName_KilometersPerHour"), GetLocalizedStringName("UnitAbbreviation_KilometersPerHour"), 3, useUSCustomary, useSI, false ),
                new OrderedUnit( UnitConverterUnits.Speed_Knot, GetLocalizedStringName("UnitName_Knot"), GetLocalizedStringName("UnitAbbreviation_Knot"), 6 ),
                new OrderedUnit( UnitConverterUnits.Speed_Mach, GetLocalizedStringName("UnitName_Mach"), GetLocalizedStringName("UnitAbbreviation_Mach"), 7 ),
                new OrderedUnit( UnitConverterUnits.Speed_MetersPerSecond, GetLocalizedStringName("UnitName_MetersPerSecond"), GetLocalizedStringName("UnitAbbreviation_MetersPerSecond"), 2 ),
                new OrderedUnit( UnitConverterUnits.Speed_MilesPerHour, GetLocalizedStringName("UnitName_MilesPerHour"), GetLocalizedStringName("UnitAbbreviation_MilesPerHour"), 5, useSI, useUSCustomary, false ),
                new OrderedUnit( UnitConverterUnits.Speed_Turtle, GetLocalizedStringName("UnitName_Turtle"), GetLocalizedStringName("UnitAbbreviation_Turtle"), 8, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Speed_Horse, GetLocalizedStringName("UnitName_Horse"), GetLocalizedStringName("UnitAbbreviation_Horse"), 9, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Speed_Jet, GetLocalizedStringName("UnitName_Jet"), GetLocalizedStringName("UnitAbbreviation_Jet"), 10, false, false, true )
            };
            unitMap.Add(ViewMode.Speed, speedUnits);

            // --- Volume Units ---
            var volumeUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.Volume_CubicCentimeter, GetLocalizedStringName("UnitName_CubicCentimeter"), GetLocalizedStringName("UnitAbbreviation_CubicCentimeter"), 2 ),
                new OrderedUnit( UnitConverterUnits.Volume_CubicFoot, GetLocalizedStringName("UnitName_CubicFoot"), GetLocalizedStringName("UnitAbbreviation_CubicFoot"), 13 ),
                new OrderedUnit( UnitConverterUnits.Volume_CubicInch, GetLocalizedStringName("UnitName_CubicInch"), GetLocalizedStringName("UnitAbbreviation_CubicInch"), 12 ),
                new OrderedUnit( UnitConverterUnits.Volume_CubicMeter, GetLocalizedStringName("UnitName_CubicMeter"), GetLocalizedStringName("UnitAbbreviation_CubicMeter"), 4 ),
                new OrderedUnit( UnitConverterUnits.Volume_CubicYard, GetLocalizedStringName("UnitName_CubicYard"), GetLocalizedStringName("UnitAbbreviation_CubicYard"), 14 ),
                new OrderedUnit( UnitConverterUnits.Volume_CupUS, GetLocalizedStringName("UnitName_CupUS"), GetLocalizedStringName("UnitAbbreviation_CupUS"), 8 ),
                new OrderedUnit( UnitConverterUnits.Volume_FluidOunceUK, GetLocalizedStringName("UnitName_FluidOunceUK"), GetLocalizedStringName("UnitAbbreviation_FluidOunceUK"), 17 ),
                new OrderedUnit( UnitConverterUnits.Volume_FluidOunceUS, GetLocalizedStringName("UnitName_FluidOunceUS"), GetLocalizedStringName("UnitAbbreviation_FluidOunceUS"), 7 ),
                new OrderedUnit( UnitConverterUnits.Volume_GallonUK, GetLocalizedStringName("UnitName_GallonUK"), GetLocalizedStringName("UnitAbbreviation_GallonUK"), 20 ),
                new OrderedUnit( UnitConverterUnits.Volume_GallonUS, GetLocalizedStringName("UnitName_GallonUS"), GetLocalizedStringName("UnitAbbreviation_GallonUS"), 11 ),
                new OrderedUnit( UnitConverterUnits.Volume_Liter, GetLocalizedStringName("UnitName_Liter"), GetLocalizedStringName("UnitAbbreviation_Liter"), 3 ),
                new OrderedUnit( UnitConverterUnits.Volume_Milliliter, GetLocalizedStringName("UnitName_Milliliter"), GetLocalizedStringName("UnitAbbreviation_Milliliter"), 1, useUSCustomary, useSI, false ),
                new OrderedUnit( UnitConverterUnits.Volume_PintUK, GetLocalizedStringName("UnitName_PintUK"), GetLocalizedStringName("UnitAbbreviation_PintUK"), 18 ),
                new OrderedUnit( UnitConverterUnits.Volume_PintUS, GetLocalizedStringName("UnitName_PintUS"), GetLocalizedStringName("UnitAbbreviation_PintUS"), 9 ),
                new OrderedUnit( UnitConverterUnits.Volume_TablespoonUS, GetLocalizedStringName("UnitName_TablespoonUS"), GetLocalizedStringName("UnitAbbreviation_TablespoonUS"), 6 ),
                new OrderedUnit( UnitConverterUnits.Volume_TeaspoonUS, GetLocalizedStringName("UnitName_TeaspoonUS"), GetLocalizedStringName("UnitAbbreviation_TeaspoonUS"), 5, useSI, useUSCustomary && m_currentRegionCode != "GB", false ),
                new OrderedUnit( UnitConverterUnits.Volume_QuartUK, GetLocalizedStringName("UnitName_QuartUK"), GetLocalizedStringName("UnitAbbreviation_QuartUK"), 19 ),
                new OrderedUnit( UnitConverterUnits.Volume_QuartUS, GetLocalizedStringName("UnitName_QuartUS"), GetLocalizedStringName("UnitAbbreviation_QuartUS"), 10 ),
                new OrderedUnit( UnitConverterUnits.Volume_TeaspoonUK, GetLocalizedStringName("UnitName_TeaspoonUK"), GetLocalizedStringName("UnitAbbreviation_TeaspoonUK"), 15, false, useUSCustomary && m_currentRegionCode == "GB", false ),
                new OrderedUnit( UnitConverterUnits.Volume_TablespoonUK, GetLocalizedStringName("UnitName_TablespoonUK"), GetLocalizedStringName("UnitAbbreviation_TablespoonUK"), 16 ),
                new OrderedUnit( UnitConverterUnits.Volume_CoffeeCup, GetLocalizedStringName("UnitName_CoffeeCup"), GetLocalizedStringName("UnitAbbreviation_CoffeeCup"), 22, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Volume_Bathtub, GetLocalizedStringName("UnitName_Bathtub"), GetLocalizedStringName("UnitAbbreviation_Bathtub"), 23, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Volume_SwimmingPool, GetLocalizedStringName("UnitName_SwimmingPool"), GetLocalizedStringName("UnitAbbreviation_SwimmingPool"), 24, false, false, true )
            };
            unitMap.Add(ViewMode.Volume, volumeUnits);

            // --- Weight Units ---
            var weightUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.Weight_Carat, GetLocalizedStringName("UnitName_Carat"), GetLocalizedStringName("UnitAbbreviation_Carat"), 1 ),
                new OrderedUnit( UnitConverterUnits.Weight_Centigram, GetLocalizedStringName("UnitName_Centigram"), GetLocalizedStringName("UnitAbbreviation_Centigram"), 3 ),
                new OrderedUnit( UnitConverterUnits.Weight_Decigram, GetLocalizedStringName("UnitName_Decigram"), GetLocalizedStringName("UnitAbbreviation_Decigram"), 4 ),
                new OrderedUnit( UnitConverterUnits.Weight_Decagram, GetLocalizedStringName("UnitName_Decagram"), GetLocalizedStringName("UnitAbbreviation_Decagram"), 6 ),
                new OrderedUnit( UnitConverterUnits.Weight_Gram, GetLocalizedStringName("UnitName_Gram"), GetLocalizedStringName("UnitAbbreviation_Gram"), 5 ),
                new OrderedUnit( UnitConverterUnits.Weight_Hectogram, GetLocalizedStringName("UnitName_Hectogram"), GetLocalizedStringName("UnitAbbreviation_Hectogram"), 7 ),
                new OrderedUnit( UnitConverterUnits.Weight_Kilogram, GetLocalizedStringName("UnitName_Kilogram"), GetLocalizedStringName("UnitAbbreviation_Kilogram"), 8, useUSCustomary, useSI, false ),
                new OrderedUnit( UnitConverterUnits.Weight_LongTon, GetLocalizedStringName("UnitName_LongTon"), GetLocalizedStringName("UnitAbbreviation_LongTon"), 14 ),
                new OrderedUnit( UnitConverterUnits.Weight_Milligram, GetLocalizedStringName("UnitName_Milligram"), GetLocalizedStringName("UnitAbbreviation_Milligram"), 2 ),
                new OrderedUnit( UnitConverterUnits.Weight_Ounce, GetLocalizedStringName("UnitName_Ounce"), GetLocalizedStringName("UnitAbbreviation_Ounce"), 10 ),
                new OrderedUnit( UnitConverterUnits.Weight_Pound, GetLocalizedStringName("UnitName_Pound"), GetLocalizedStringName("UnitAbbreviation_Pound"), 11, useSI, useUSCustomary, false ),
                new OrderedUnit( UnitConverterUnits.Weight_ShortTon, GetLocalizedStringName("UnitName_ShortTon"), GetLocalizedStringName("UnitAbbreviation_ShortTon"), 13 ),
                new OrderedUnit( UnitConverterUnits.Weight_Stone, GetLocalizedStringName("UnitName_Stone"), GetLocalizedStringName("UnitAbbreviation_Stone"), 12 ),
                new OrderedUnit( UnitConverterUnits.Weight_Tonne, GetLocalizedStringName("UnitName_Tonne"), GetLocalizedStringName("UnitAbbreviation_Tonne"), 9 ),
                new OrderedUnit( UnitConverterUnits.Weight_Snowflake, GetLocalizedStringName("UnitName_Snowflake"), GetLocalizedStringName("UnitAbbreviation_Snowflake"), 15, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Weight_SoccerBall, GetLocalizedStringName("UnitName_SoccerBall"), GetLocalizedStringName("UnitAbbreviation_SoccerBall"), 16, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Weight_Elephant, GetLocalizedStringName("UnitName_Elephant"), GetLocalizedStringName("UnitAbbreviation_Elephant"), 17, false, false, true ),
                new OrderedUnit( UnitConverterUnits.Weight_Whale, GetLocalizedStringName("UnitName_Whale"), GetLocalizedStringName("UnitAbbreviation_Whale"), 18, false, false, true )
            };
            unitMap.Add(ViewMode.Weight, weightUnits);

            // --- Pressure Units ---
            var pressureUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.Pressure_Atmosphere, GetLocalizedStringName("UnitName_Atmosphere"), GetLocalizedStringName("UnitAbbreviation_Atmosphere"), 1, true, false, false ),
                new OrderedUnit( UnitConverterUnits.Pressure_Bar, GetLocalizedStringName("UnitName_Bar"), GetLocalizedStringName("UnitAbbreviation_Bar"), 2, false, true, false ),
                new OrderedUnit( UnitConverterUnits.Pressure_KiloPascal, GetLocalizedStringName("UnitName_KiloPascal"), GetLocalizedStringName("UnitAbbreviation_KiloPascal"), 3 ),
                new OrderedUnit( UnitConverterUnits.Pressure_MillimeterOfMercury, GetLocalizedStringName("UnitName_MillimeterOfMercury "), GetLocalizedStringName("UnitAbbreviation_MillimeterOfMercury "), 4 ), // Note trailing space in C++ resource keys
                new OrderedUnit( UnitConverterUnits.Pressure_Pascal, GetLocalizedStringName("UnitName_Pascal"), GetLocalizedStringName("UnitAbbreviation_Pascal"), 5 ),
                new OrderedUnit( UnitConverterUnits.Pressure_PSI, GetLocalizedStringName("UnitName_PSI"), GetLocalizedStringName("UnitAbbreviation_PSI"), 6, false, false, false )
            };
            unitMap.Add(ViewMode.Pressure, pressureUnits);

            // --- Angle Units ---
            var angleUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.Angle_Degree, GetLocalizedStringName("UnitName_Degree"), GetLocalizedStringName("UnitAbbreviation_Degree"), 1, true, false, false ),
                new OrderedUnit( UnitConverterUnits.Angle_Radian, GetLocalizedStringName("UnitName_Radian"), GetLocalizedStringName("UnitAbbreviation_Radian"), 2, false, true, false ),
                new OrderedUnit( UnitConverterUnits.Angle_Gradian, GetLocalizedStringName("UnitName_Gradian"), GetLocalizedStringName("UnitAbbreviation_Gradian"), 3 )
            };
            unitMap.Add(ViewMode.Angle, angleUnits);

            return unitMap;
        }

        private Dictionary<ViewMode, Dictionary<int, double>> GetConversionData()
        {
            /*categoryId, UnitId, factor*/
            var unitDataList = new List<UnitData> {
                new UnitData { categoryId = ViewMode.Area, unitId = UnitConverterUnits.Area_Acre, factor = 4046.8564224 },
                new UnitData { categoryId = ViewMode.Area, unitId = UnitConverterUnits.Area_SquareMeter, factor = 1 },
                new UnitData { categoryId = ViewMode.Area, unitId = UnitConverterUnits.Area_SquareFoot, factor = 0.09290304 },
                new UnitData { categoryId = ViewMode.Area, unitId = UnitConverterUnits.Area_SquareYard, factor = 0.83612736 },
                new UnitData { categoryId = ViewMode.Area, unitId = UnitConverterUnits.Area_SquareMillimeter, factor = 0.000001 },
                new UnitData { categoryId = ViewMode.Area, unitId = UnitConverterUnits.Area_SquareCentimeter, factor = 0.0001 },
                new UnitData { categoryId = ViewMode.Area, unitId = UnitConverterUnits.Area_SquareInch, factor = 0.00064516 },
                new UnitData { categoryId = ViewMode.Area, unitId = UnitConverterUnits.Area_SquareMile, factor = 2589988.110336 },
                new UnitData { categoryId = ViewMode.Area, unitId = UnitConverterUnits.Area_SquareKilometer, factor = 1000000 },
                new UnitData { categoryId = ViewMode.Area, unitId = UnitConverterUnits.Area_Hectare, factor = 10000 },
                new UnitData { categoryId = ViewMode.Area, unitId = UnitConverterUnits.Area_Hand, factor = 0.012516104 },
                new UnitData { categoryId = ViewMode.Area, unitId = UnitConverterUnits.Area_Paper, factor = 0.06032246 },
                new UnitData { categoryId = ViewMode.Area, unitId = UnitConverterUnits.Area_SoccerField, factor = 10869.66 },
                new UnitData { categoryId = ViewMode.Area, unitId = UnitConverterUnits.Area_Castle, factor = 100000 },
                new UnitData { categoryId = ViewMode.Area, unitId = UnitConverterUnits.Area_Pyeong, factor = 400.0 / 121.0 },

                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Bit, factor = 0.000000125 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Nibble, factor = 0.0000005 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Byte, factor = 0.000001 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Kilobyte, factor = 0.001 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Megabyte, factor = 1 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Gigabyte, factor = 1000 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Terabyte, factor = 1000000 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Petabyte, factor = 1000000000 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Exabytes, factor = 1000000000000 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Zetabytes, factor = 1000000000000000 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Yottabyte, factor = 1000000000000000000 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Kilobit, factor = 0.000125 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Megabit, factor = 0.125 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Gigabit, factor = 125 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Terabit, factor = 125000 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Petabit, factor = 125000000 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Exabits, factor = 125000000000 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Zetabits, factor = 125000000000000 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Yottabit, factor = 125000000000000000 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Gibibits, factor = 134.217728 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Gibibytes, factor = 1073.741824 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Kibibits, factor = 0.000128 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Kibibytes, factor = 0.001024 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Mebibits, factor = 0.131072 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Mebibytes, factor = 1.048576 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Pebibits, factor = 140737488.355328 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Pebibytes, factor = 1125899906.842624 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Tebibits, factor = 137438.953472 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Tebibytes, factor = 1099511.627776 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Exbibits, factor = 144115188075.855872 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Exbibytes, factor = 1152921504606.846976 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Zebibits, factor = 147573952589676.412928 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Zebibytes, factor = 1180591620717411.303424 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Yobibits, factor = 151115727451828646.838272 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_Yobibytes, factor = 1208925819614629174.706176 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_FloppyDisk, factor = 1.474560 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_CD, factor = 700 },
                new UnitData { categoryId = ViewMode.Data, unitId = UnitConverterUnits.Data_DVD, factor = 4700 },

                new UnitData { categoryId = ViewMode.Energy, unitId = UnitConverterUnits.Energy_Calorie, factor = 4.184 },
                new UnitData { categoryId = ViewMode.Energy, unitId = UnitConverterUnits.Energy_Kilocalorie, factor = 4184 },
                new UnitData { categoryId = ViewMode.Energy, unitId = UnitConverterUnits.Energy_BritishThermalUnit, factor = 1055.056 },
                new UnitData { categoryId = ViewMode.Energy, unitId = UnitConverterUnits.Energy_Kilojoule, factor = 1000 },
                new UnitData { categoryId = ViewMode.Energy, unitId = UnitConverterUnits.Energy_Kilowatthour, factor = 3600000 },
                new UnitData { categoryId = ViewMode.Energy, unitId = UnitConverterUnits.Energy_ElectronVolt, factor = 0.0000000000000000001602176565 },
                new UnitData { categoryId = ViewMode.Energy, unitId = UnitConverterUnits.Energy_Joule, factor = 1 },
                new UnitData { categoryId = ViewMode.Energy, unitId = UnitConverterUnits.Energy_FootPound, factor = 1.3558179483314 },
                new UnitData { categoryId = ViewMode.Energy, unitId = UnitConverterUnits.Energy_Battery, factor = 9000 },
                new UnitData { categoryId = ViewMode.Energy, unitId = UnitConverterUnits.Energy_Banana, factor = 439614 },
                new UnitData { categoryId = ViewMode.Energy, unitId = UnitConverterUnits.Energy_SliceOfCake, factor = 1046700 },

                new UnitData { categoryId = ViewMode.Length, unitId = UnitConverterUnits.Length_Inch, factor = 0.0254 },
                new UnitData { categoryId = ViewMode.Length, unitId = UnitConverterUnits.Length_Foot, factor = 0.3048 },
                new UnitData { categoryId = ViewMode.Length, unitId = UnitConverterUnits.Length_Yard, factor = 0.9144 },
                new UnitData { categoryId = ViewMode.Length, unitId = UnitConverterUnits.Length_Mile, factor = 1609.344 },
                new UnitData { categoryId = ViewMode.Length, unitId = UnitConverterUnits.Length_Micron, factor = 0.000001 },
                new UnitData { categoryId = ViewMode.Length, unitId = UnitConverterUnits.Length_Millimeter, factor = 0.001 },
                new UnitData { categoryId = ViewMode.Length, unitId = UnitConverterUnits.Length_Nanometer, factor = 0.000000001 },
                new UnitData { categoryId = ViewMode.Length, unitId = UnitConverterUnits.Length_Angstrom, factor = 0.0000000001 },
                new UnitData { categoryId = ViewMode.Length, unitId = UnitConverterUnits.Length_Centimeter, factor = 0.01 },
                new UnitData { categoryId = ViewMode.Length, unitId = UnitConverterUnits.Length_Meter, factor = 1 },
                new UnitData { categoryId = ViewMode.Length, unitId = UnitConverterUnits.Length_Kilometer, factor = 1000 },
                new UnitData { categoryId = ViewMode.Length, unitId = UnitConverterUnits.Length_NauticalMile, factor = 1852 },
                new UnitData { categoryId = ViewMode.Length, unitId = UnitConverterUnits.Length_Paperclip, factor = 0.035052 },
                new UnitData { categoryId = ViewMode.Length, unitId = UnitConverterUnits.Length_Hand, factor = 0.18669 },
                new UnitData { categoryId = ViewMode.Length, unitId = UnitConverterUnits.Length_JumboJet, factor = 76 },

                new UnitData { categoryId = ViewMode.Power, unitId = UnitConverterUnits.Power_BritishThermalUnitPerMinute, factor = 17.58426666666667 },
                new UnitData { categoryId = ViewMode.Power, unitId = UnitConverterUnits.Power_FootPoundPerMinute, factor = 0.0225969658055233 },
                new UnitData { categoryId = ViewMode.Power, unitId = UnitConverterUnits.Power_Watt, factor = 1 },
                new UnitData { categoryId = ViewMode.Power, unitId = UnitConverterUnits.Power_Kilowatt, factor = 1000 },
                new UnitData { categoryId = ViewMode.Power, unitId = UnitConverterUnits.Power_Horsepower, factor = 745.69987158227022 },
                new UnitData { categoryId = ViewMode.Power, unitId = UnitConverterUnits.Power_LightBulb, factor = 60 },
                new UnitData { categoryId = ViewMode.Power, unitId = UnitConverterUnits.Power_Horse, factor = 745.7 },
                new UnitData { categoryId = ViewMode.Power, unitId = UnitConverterUnits.Power_TrainEngine, factor = 2982799.486329081 },

                new UnitData { categoryId = ViewMode.Time, unitId = UnitConverterUnits.Time_Day, factor = 86400 },
                new UnitData { categoryId = ViewMode.Time, unitId = UnitConverterUnits.Time_Second, factor = 1 },
                new UnitData { categoryId = ViewMode.Time, unitId = UnitConverterUnits.Time_Week, factor = 604800 },
                new UnitData { categoryId = ViewMode.Time, unitId = UnitConverterUnits.Time_Year, factor = 31557600 },
                new UnitData { categoryId = ViewMode.Time, unitId = UnitConverterUnits.Time_Millisecond, factor = 0.001 },
                new UnitData { categoryId = ViewMode.Time, unitId = UnitConverterUnits.Time_Microsecond, factor = 0.000001 },
                new UnitData { categoryId = ViewMode.Time, unitId = UnitConverterUnits.Time_Minute, factor = 60 },
                new UnitData { categoryId = ViewMode.Time, unitId = UnitConverterUnits.Time_Hour, factor = 3600 },

                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_CupUS, factor = 236.588237 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_PintUS, factor = 473.176473 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_PintUK, factor = 568.26125 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_QuartUS, factor = 946.352946 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_QuartUK, factor = 1136.5225 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_GallonUS, factor = 3785.411784 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_GallonUK, factor = 4546.09 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_Liter, factor = 1000 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_TeaspoonUS, factor = 4.92892159375 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_TablespoonUS, factor = 14.78676478125 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_CubicCentimeter, factor = 1 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_CubicYard, factor = 764554.857984 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_CubicMeter, factor = 1000000 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_Milliliter, factor = 1 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_CubicInch, factor = 16.387064 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_CubicFoot, factor = 28316.846592 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_FluidOunceUS, factor = 29.5735295625 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_FluidOunceUK, factor = 28.4130625 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_TeaspoonUK, factor = 5.91938802083333333333 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_TablespoonUK, factor = 17.7581640625 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_CoffeeCup, factor = 236.5882 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_Bathtub, factor = 378541.2 },
                new UnitData { categoryId = ViewMode.Volume, unitId = UnitConverterUnits.Volume_SwimmingPool, factor = 3750000000 },

                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_Kilogram, factor = 1 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_Hectogram, factor = 0.1 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_Decagram, factor = 0.01 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_Gram, factor = 0.001 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_Pound, factor = 0.45359237 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_Ounce, factor = 0.028349523125 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_Milligram, factor = 0.000001 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_Centigram, factor = 0.00001 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_Decigram, factor = 0.0001 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_LongTon, factor = 1016.0469088 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_Tonne, factor = 1000 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_Stone, factor = 6.35029318 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_Carat, factor = 0.0002 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_ShortTon, factor = 907.18474 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_Snowflake, factor = 0.000002 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_SoccerBall, factor = 0.4325 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_Elephant, factor = 4000 },
                new UnitData { categoryId = ViewMode.Weight, unitId = UnitConverterUnits.Weight_Whale, factor = 90000 },

                new UnitData { categoryId = ViewMode.Speed, unitId = UnitConverterUnits.Speed_CentimetersPerSecond, factor = 1 },
                new UnitData { categoryId = ViewMode.Speed, unitId = UnitConverterUnits.Speed_FeetPerSecond, factor = 30.48 },
                new UnitData { categoryId = ViewMode.Speed, unitId = UnitConverterUnits.Speed_KilometersPerHour, factor = 27.777777777777777777778 },
                new UnitData { categoryId = ViewMode.Speed, unitId = UnitConverterUnits.Speed_Knot, factor = 51.44 },
                new UnitData { categoryId = ViewMode.Speed, unitId = UnitConverterUnits.Speed_Mach, factor = 34030 },
                new UnitData { categoryId = ViewMode.Speed, unitId = UnitConverterUnits.Speed_MetersPerSecond, factor = 100 },
                new UnitData { categoryId = ViewMode.Speed, unitId = UnitConverterUnits.Speed_MilesPerHour, factor = 44.7 },
                new UnitData { categoryId = ViewMode.Speed, unitId = UnitConverterUnits.Speed_Turtle, factor = 8.94 },
                new UnitData { categoryId = ViewMode.Speed, unitId = UnitConverterUnits.Speed_Horse, factor = 2011.5 },
                new UnitData { categoryId = ViewMode.Speed, unitId = UnitConverterUnits.Speed_Jet, factor = 24585 },

                new UnitData { categoryId = ViewMode.Angle, unitId = UnitConverterUnits.Angle_Degree, factor = 1 },
                new UnitData { categoryId = ViewMode.Angle, unitId = UnitConverterUnits.Angle_Radian, factor = 57.29577951308233 },
                new UnitData { categoryId = ViewMode.Angle, unitId = UnitConverterUnits.Angle_Gradian, factor = 0.9 },

                new UnitData { categoryId = ViewMode.Pressure, unitId = UnitConverterUnits.Pressure_Atmosphere, factor = 1 },
                new UnitData { categoryId = ViewMode.Pressure, unitId = UnitConverterUnits.Pressure_Bar, factor = 0.9869232667160128 },
                new UnitData { categoryId = ViewMode.Pressure, unitId = UnitConverterUnits.Pressure_KiloPascal, factor = 0.0098692326671601 },
                new UnitData { categoryId = ViewMode.Pressure, unitId = UnitConverterUnits.Pressure_MillimeterOfMercury, factor = 0.0013155687145324 },
                new UnitData { categoryId = ViewMode.Pressure, unitId = UnitConverterUnits.Pressure_Pascal, factor = 9.869232667160128e-6 },
                new UnitData { categoryId = ViewMode.Pressure, unitId = UnitConverterUnits.Pressure_PSI, factor = 0.068045961016531 }
            };

            var categoryToUnitConversionMap = new Dictionary<ViewMode, Dictionary<int, double>>();

            // Populate the hash map and return;
            foreach (UnitData unitdata in unitDataList)
            {
                if (!categoryToUnitConversionMap.TryGetValue(unitdata.categoryId, out Dictionary<int, double>? conversionData))
                {
                    conversionData = new Dictionary<int, double>();
                    categoryToUnitConversionMap.Add(unitdata.categoryId, conversionData);
                }
                conversionData.Add((int)unitdata.unitId, unitdata.factor);
            }
            return categoryToUnitConversionMap;
        }

        private Dictionary<int, Dictionary<int, CalcManager.ConversionData>> GetExplicitConversionData()
        {
            /* categoryId, ParentUnitId, UnitId, ratio, offset, offsetfirst*/
            var conversionDataList = new ExplicitUnitConversionData[] {
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.Temperature_DegreesCelsius, UnitConverterUnits.Temperature_DegreesCelsius, 1, 0 ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.Temperature_DegreesCelsius, UnitConverterUnits.Temperature_DegreesFahrenheit, 1.8, 32 ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.Temperature_DegreesCelsius, UnitConverterUnits.Temperature_Kelvin, 1, 273.15 ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.Temperature_DegreesFahrenheit, UnitConverterUnits.Temperature_DegreesCelsius, 0.55555555555555555555555555555556, -32, CONVERT_WITH_OFFSET_FIRST ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.Temperature_DegreesFahrenheit, UnitConverterUnits.Temperature_DegreesFahrenheit, 1, 0 ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.Temperature_DegreesFahrenheit, UnitConverterUnits.Temperature_Kelvin, 0.55555555555555555555555555555556, 459.67, CONVERT_WITH_OFFSET_FIRST ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.Temperature_Kelvin, UnitConverterUnits.Temperature_DegreesCelsius, 1, -273.15, CONVERT_WITH_OFFSET_FIRST ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.Temperature_Kelvin, UnitConverterUnits.Temperature_DegreesFahrenheit, 1.8, -459.67 ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.Temperature_Kelvin, UnitConverterUnits.Temperature_Kelvin, 1, 0 )
            };

            var unitToUnitConversionList = new Dictionary<int, Dictionary<int, CalcManager.ConversionData>>();

            // Populate the hash map and return;
            foreach (ExplicitUnitConversionData data in conversionDataList)
            {
                 if (!unitToUnitConversionList.TryGetValue((int)data.parentUnitId, out Dictionary<int, CalcManager.ConversionData>? conversionData))
                {
                    conversionData = new Dictionary<int, CalcManager.ConversionData>();
                    unitToUnitConversionList.Add((int)data.parentUnitId, conversionData);
                }
                conversionData.Add((int)data.unitId, data);
            }
            return unitToUnitConversionList;
        }

        private string GetLocalizedStringName(string stringId)
        {
            return AppResourceProvider.GetInstance().GetResourceString(stringId);
        }
    }
}
