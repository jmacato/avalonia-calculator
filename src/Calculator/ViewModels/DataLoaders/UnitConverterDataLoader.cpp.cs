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
            System.ArgumentNullException.ThrowIfNull(category);
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
            Dictionary<ViewMode, Dictionary<int, string>> categoryToUnitConversionDataMap = GetConversionData();
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
                orderedUnits.Sort((first, second) => first.Order.CompareTo(second.Order));

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

                    if (!explicitConversionData.TryGetValue(
                            unit.Id,
                            out Dictionary<int, CalcManager.ConversionData>? explicitConversions))
                    {
                        // Get the associated units for a category id
                        if (!categoryToUnitConversionDataMap.TryGetValue(categoryViewMode, out Dictionary<int, string>? unitConversions))
                        {
                            Debug.WriteLine($"Warning: No conversion data found for category {categoryViewMode}");
                            unitConversions = new Dictionary<int, string>();
                        }

                        if (!unitConversions.TryGetValue(unit.Id, out string? unitFactor))
                        {
                            Debug.WriteLine($"Warning: Unit factor not found for unit {unit.Id} in category {categoryViewMode}");
                            continue;
                        }

                        foreach (var kvp in unitConversions)
                        {
                            int id = kvp.Key;
                            string conversionFactor = kvp.Value;

                            if (!idToUnit.ContainsKey(id))
                            {
                                // Optional units will not be in idToUnit but can be in unitConversions.
                                // For optional units that did not make it to the current set of units, just continue.
                                continue;
                            }

                            var parsedData = new CalcManager.ConversionData(
                                unitFactor,
                                conversionFactor,
                                "0",
                                false);
                            conversions.Add(idToUnit[id], parsedData);
                        }
                    }
                    else
                    {
                        foreach (var kvp in explicitConversions)
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
                new OrderedUnit( UnitConverterUnits.AreaAcre, GetLocalizedStringName("UnitName_Acre"), GetLocalizedStringName("UnitAbbreviation_Acre"), 9 ),
                new OrderedUnit( UnitConverterUnits.AreaHectare, GetLocalizedStringName("UnitName_Hectare"), GetLocalizedStringName("UnitAbbreviation_Hectare"), 4 ),
                new OrderedUnit( UnitConverterUnits.AreaSquareCentimeter, GetLocalizedStringName("UnitName_SquareCentimeter"), GetLocalizedStringName("UnitAbbreviation_SquareCentimeter"), 2 ),
                new OrderedUnit( UnitConverterUnits.AreaSquareFoot, GetLocalizedStringName("UnitName_SquareFoot"), GetLocalizedStringName("UnitAbbreviation_SquareFoot"), 7, useSI, useUSCustomary, false ),
                new OrderedUnit( UnitConverterUnits.AreaSquareInch, GetLocalizedStringName("UnitName_SquareInch"), GetLocalizedStringName("UnitAbbreviation_SquareInch"), 6 ),
                new OrderedUnit( UnitConverterUnits.AreaSquareKilometer, GetLocalizedStringName("UnitName_SquareKilometer"), GetLocalizedStringName("UnitAbbreviation_SquareKilometer"), 5 ),
                new OrderedUnit( UnitConverterUnits.AreaSquareMeter, GetLocalizedStringName("UnitName_SquareMeter"), GetLocalizedStringName("UnitAbbreviation_SquareMeter"), 3, useUSCustomary, useSI, false ),
                new OrderedUnit( UnitConverterUnits.AreaSquareMile, GetLocalizedStringName("UnitName_SquareMile"), GetLocalizedStringName("UnitAbbreviation_SquareMile"), 10 ),
                new OrderedUnit( UnitConverterUnits.AreaSquareMillimeter, GetLocalizedStringName("UnitName_SquareMillimeter"), GetLocalizedStringName("UnitAbbreviation_SquareMillimeter"), 1 ),
                new OrderedUnit( UnitConverterUnits.AreaSquareYard, GetLocalizedStringName("UnitName_SquareYard"), GetLocalizedStringName("UnitAbbreviation_SquareYard"), 8 ),
                new OrderedUnit( UnitConverterUnits.AreaHand, GetLocalizedStringName("UnitName_Hand"), GetLocalizedStringName("UnitAbbreviation_Hand"), 11, false, false, true ),
                new OrderedUnit( UnitConverterUnits.AreaPaper, GetLocalizedStringName("UnitName_Paper"), GetLocalizedStringName("UnitAbbreviation_Paper"), 12, false, false, true ),
                new OrderedUnit( UnitConverterUnits.AreaSoccerField, GetLocalizedStringName("UnitName_SoccerField"), GetLocalizedStringName("UnitAbbreviation_SoccerField"), 13, false, false, true ),
                new OrderedUnit( UnitConverterUnits.AreaCastle, GetLocalizedStringName("UnitName_Castle"), GetLocalizedStringName("UnitAbbreviation_Castle"), 14, false, false, true )
            };
            if (usePyeong)
            {
                areaUnits.Add(new OrderedUnit(UnitConverterUnits.AreaPyeong, GetLocalizedStringName("UnitName_Pyeong"), GetLocalizedStringName("UnitAbbreviation_Pyeong"), 15, false, false, false));
            }
            unitMap.Add(ViewMode.Area, areaUnits);

            // --- Data Units ---
            var dataUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.DataBit, GetLocalizedStringName("UnitName_Bit"), GetLocalizedStringName("UnitAbbreviation_Bit"), 1 ),
                new OrderedUnit( UnitConverterUnits.DataByte, GetLocalizedStringName("UnitName_Byte"), GetLocalizedStringName("UnitAbbreviation_Byte"), 3 ),
                new OrderedUnit( UnitConverterUnits.DataExabits, GetLocalizedStringName("UnitName_Exabits"), GetLocalizedStringName("UnitAbbreviation_Exabits"), 24 ),
                new OrderedUnit( UnitConverterUnits.DataExabytes, GetLocalizedStringName("UnitName_Exabytes"), GetLocalizedStringName("UnitAbbreviation_Exabytes"), 26 ),
                new OrderedUnit( UnitConverterUnits.DataExbibits, GetLocalizedStringName("UnitName_Exbibits"), GetLocalizedStringName("UnitAbbreviation_Exbibits"), 25 ),
                new OrderedUnit( UnitConverterUnits.DataExbibytes, GetLocalizedStringName("UnitName_Exbibytes"), GetLocalizedStringName("UnitAbbreviation_Exbibytes"), 27 ),
                new OrderedUnit( UnitConverterUnits.DataGibibits, GetLocalizedStringName("UnitName_Gibibits"), GetLocalizedStringName("UnitAbbreviation_Gibibits"), 13 ),
                new OrderedUnit( UnitConverterUnits.DataGibibytes, GetLocalizedStringName("UnitName_Gibibytes"), GetLocalizedStringName("UnitAbbreviation_Gibibytes"), 15 ),
                new OrderedUnit( UnitConverterUnits.DataGigabit, GetLocalizedStringName("UnitName_Gigabit"), GetLocalizedStringName("UnitAbbreviation_Gigabit"), 12 ),
                new OrderedUnit( UnitConverterUnits.DataGigabyte, GetLocalizedStringName("UnitName_Gigabyte"), GetLocalizedStringName("UnitAbbreviation_Gigabyte"), 14, true, false, false ),
                new OrderedUnit( UnitConverterUnits.DataKibibits, GetLocalizedStringName("UnitName_Kibibits"), GetLocalizedStringName("UnitAbbreviation_Kibibits"), 5 ),
                new OrderedUnit( UnitConverterUnits.DataKibibytes, GetLocalizedStringName("UnitName_Kibibytes"), GetLocalizedStringName("UnitAbbreviation_Kibibytes"), 7 ),
                new OrderedUnit( UnitConverterUnits.DataKilobit, GetLocalizedStringName("UnitName_Kilobit"), GetLocalizedStringName("UnitAbbreviation_Kilobit"), 4 ),
                new OrderedUnit( UnitConverterUnits.DataKilobyte, GetLocalizedStringName("UnitName_Kilobyte"), GetLocalizedStringName("UnitAbbreviation_Kilobyte"), 6 ),
                new OrderedUnit( UnitConverterUnits.DataMebibits, GetLocalizedStringName("UnitName_Mebibits"), GetLocalizedStringName("UnitAbbreviation_Mebibits"), 9 ),
                new OrderedUnit( UnitConverterUnits.DataMebibytes, GetLocalizedStringName("UnitName_Mebibytes"), GetLocalizedStringName("UnitAbbreviation_Mebibytes"), 11 ),
                new OrderedUnit( UnitConverterUnits.DataMegabit, GetLocalizedStringName("UnitName_Megabit"), GetLocalizedStringName("UnitAbbreviation_Megabit"), 8 ),
                new OrderedUnit( UnitConverterUnits.DataMegabyte, GetLocalizedStringName("UnitName_Megabyte"), GetLocalizedStringName("UnitAbbreviation_Megabyte"), 10, false, true, false ),
                new OrderedUnit( UnitConverterUnits.DataNibble, GetLocalizedStringName("UnitName_Nibble"), GetLocalizedStringName("UnitAbbreviation_Nibble"), 2 ),
                new OrderedUnit( UnitConverterUnits.DataPebibits, GetLocalizedStringName("UnitName_Pebibits"), GetLocalizedStringName("UnitAbbreviation_Pebibits"), 21 ),
                new OrderedUnit( UnitConverterUnits.DataPebibytes, GetLocalizedStringName("UnitName_Pebibytes"), GetLocalizedStringName("UnitAbbreviation_Pebibytes"), 23 ),
                new OrderedUnit( UnitConverterUnits.DataPetabit, GetLocalizedStringName("UnitName_Petabit"), GetLocalizedStringName("UnitAbbreviation_Petabit"), 20 ),
                new OrderedUnit( UnitConverterUnits.DataPetabyte, GetLocalizedStringName("UnitName_Petabyte"), GetLocalizedStringName("UnitAbbreviation_Petabyte"), 22 ),
                new OrderedUnit( UnitConverterUnits.DataTebibits, GetLocalizedStringName("UnitName_Tebibits"), GetLocalizedStringName("UnitAbbreviation_Tebibits"), 17 ),
                new OrderedUnit( UnitConverterUnits.DataTebibytes, GetLocalizedStringName("UnitName_Tebibytes"), GetLocalizedStringName("UnitAbbreviation_Tebibytes"), 19 ),
                new OrderedUnit( UnitConverterUnits.DataTerabit, GetLocalizedStringName("UnitName_Terabit"), GetLocalizedStringName("UnitAbbreviation_Terabit"), 16 ),
                new OrderedUnit( UnitConverterUnits.DataTerabyte, GetLocalizedStringName("UnitName_Terabyte"), GetLocalizedStringName("UnitAbbreviation_Terabyte"), 18 ),
                new OrderedUnit( UnitConverterUnits.DataYobibits, GetLocalizedStringName("UnitName_Yobibits"), GetLocalizedStringName("UnitAbbreviation_Yobibits"), 33 ),
                new OrderedUnit( UnitConverterUnits.DataYobibytes, GetLocalizedStringName("UnitName_Yobibytes"), GetLocalizedStringName("UnitAbbreviation_Yobibytes"), 35 ),
                new OrderedUnit( UnitConverterUnits.DataYottabit, GetLocalizedStringName("UnitName_Yottabit"), GetLocalizedStringName("UnitAbbreviation_Yottabit"), 32 ),
                new OrderedUnit( UnitConverterUnits.DataYottabyte, GetLocalizedStringName("UnitName_Yottabyte"), GetLocalizedStringName("UnitAbbreviation_Yottabyte"), 34 ),
                new OrderedUnit( UnitConverterUnits.DataZebibits, GetLocalizedStringName("UnitName_Zebibits"), GetLocalizedStringName("UnitAbbreviation_Zebibits"), 29 ),
                new OrderedUnit( UnitConverterUnits.DataZebibytes, GetLocalizedStringName("UnitName_Zebibytes"), GetLocalizedStringName("UnitAbbreviation_Zebibytes"), 31 ),
                new OrderedUnit( UnitConverterUnits.DataZetabits, GetLocalizedStringName("UnitName_Zetabits"), GetLocalizedStringName("UnitAbbreviation_Zetabits"), 28 ),
                new OrderedUnit( UnitConverterUnits.DataZetabytes, GetLocalizedStringName("UnitName_Zetabytes"), GetLocalizedStringName("UnitAbbreviation_Zetabytes"), 30 ),
                new OrderedUnit( UnitConverterUnits.DataFloppyDisk, GetLocalizedStringName("UnitName_FloppyDisk"), GetLocalizedStringName("UnitAbbreviation_FloppyDisk"), 13, false, false, true ),
                new OrderedUnit( UnitConverterUnits.DataCD, GetLocalizedStringName("UnitName_CD"), GetLocalizedStringName("UnitAbbreviation_CD"), 14, false, false, true ),
                new OrderedUnit( UnitConverterUnits.DataDVD, GetLocalizedStringName("UnitName_DVD"), GetLocalizedStringName("UnitAbbreviation_DVD"), 15, false, false, true )
            };
            unitMap.Add(ViewMode.Data, dataUnits);

            // --- Energy Units ---
            var energyUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.EnergyBritishThermalUnit, GetLocalizedStringName("UnitName_BritishThermalUnit"), GetLocalizedStringName("UnitAbbreviation_BritishThermalUnit"), 7 ),
                new OrderedUnit( UnitConverterUnits.EnergyCalorie, GetLocalizedStringName("UnitName_Calorie"), GetLocalizedStringName("UnitAbbreviation_Calorie"), 4 ),
                new OrderedUnit( UnitConverterUnits.EnergyElectronVolt, GetLocalizedStringName("UnitName_Electron-Volt"), GetLocalizedStringName("UnitAbbreviation_Electron-Volt"), 1 ),
                new OrderedUnit( UnitConverterUnits.EnergyFootPound, GetLocalizedStringName("UnitName_Foot-Pound"), GetLocalizedStringName("UnitAbbreviation_Foot-Pound"), 6 ),
                new OrderedUnit( UnitConverterUnits.EnergyJoule, GetLocalizedStringName("UnitName_Joule"), GetLocalizedStringName("UnitAbbreviation_Joule"), 2, true, false, false ),
                new OrderedUnit( UnitConverterUnits.EnergyKilowatthour, GetLocalizedStringName("UnitName_Kilowatthour"), GetLocalizedStringName("UnitAbbreviation_Kilowatthour"), 166, true, false, false ),
                new OrderedUnit( UnitConverterUnits.EnergyKilocalorie, GetLocalizedStringName("UnitName_Kilocalorie"), GetLocalizedStringName("UnitAbbreviation_Kilocalorie"), 5, false, true, false ),
                new OrderedUnit( UnitConverterUnits.EnergyKilojoule, GetLocalizedStringName("UnitName_Kilojoule"), GetLocalizedStringName("UnitAbbreviation_Kilojoule"), 3 ),
                new OrderedUnit( UnitConverterUnits.EnergyBattery, GetLocalizedStringName("UnitName_Battery"), GetLocalizedStringName("UnitAbbreviation_Battery"), 8, false, false, true ),
                new OrderedUnit( UnitConverterUnits.EnergyBanana, GetLocalizedStringName("UnitName_Banana"), GetLocalizedStringName("UnitAbbreviation_Banana"), 9, false, false, true ),
                new OrderedUnit( UnitConverterUnits.EnergySliceOfCake, GetLocalizedStringName("UnitName_SliceOfCake"), GetLocalizedStringName("UnitAbbreviation_SliceOfCake"), 10, false, false, true )
            };
            unitMap.Add(ViewMode.Energy, energyUnits);

            // --- Length Units ---
            var lengthUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.LengthAngstrom, GetLocalizedStringName("UnitName_Angstrom"), GetLocalizedStringName("UnitAbbreviation_Angstrom"), 1 ),
                new OrderedUnit( UnitConverterUnits.LengthCentimeter, GetLocalizedStringName("UnitName_Centimeter"), GetLocalizedStringName("UnitAbbreviation_Centimeter"), 5, useUSCustomary, useSI, false ),
                new OrderedUnit( UnitConverterUnits.LengthFoot, GetLocalizedStringName("UnitName_Foot"), GetLocalizedStringName("UnitAbbreviation_Foot"), 9 ),
                new OrderedUnit( UnitConverterUnits.LengthInch, GetLocalizedStringName("UnitName_Inch"), GetLocalizedStringName("UnitAbbreviation_Inch"), 8, useSI, useUSCustomary, false ),
                new OrderedUnit( UnitConverterUnits.LengthKilometer, GetLocalizedStringName("UnitName_Kilometer"), GetLocalizedStringName("UnitAbbreviation_Kilometer"), 7 ),
                new OrderedUnit( UnitConverterUnits.LengthMeter, GetLocalizedStringName("UnitName_Meter"), GetLocalizedStringName("UnitAbbreviation_Meter"), 6 ),
                new OrderedUnit( UnitConverterUnits.LengthMicron, GetLocalizedStringName("UnitName_Micron"), GetLocalizedStringName("UnitAbbreviation_Micron"), 3 ),
                new OrderedUnit( UnitConverterUnits.LengthMile, GetLocalizedStringName("UnitName_Mile"), GetLocalizedStringName("UnitAbbreviation_Mile"), 11 ),
                new OrderedUnit( UnitConverterUnits.LengthMillimeter, GetLocalizedStringName("UnitName_Millimeter"), GetLocalizedStringName("UnitAbbreviation_Millimeter"), 4 ),
                new OrderedUnit( UnitConverterUnits.LengthNanometer, GetLocalizedStringName("UnitName_Nanometer"), GetLocalizedStringName("UnitAbbreviation_Nanometer"), 2 ),
                new OrderedUnit( UnitConverterUnits.LengthNauticalMile, GetLocalizedStringName("UnitName_NauticalMile"), GetLocalizedStringName("UnitAbbreviation_NauticalMile"), 12 ),
                new OrderedUnit( UnitConverterUnits.LengthYard, GetLocalizedStringName("UnitName_Yard"), GetLocalizedStringName("UnitAbbreviation_Yard"), 10 ),
                new OrderedUnit( UnitConverterUnits.LengthPaperclip, GetLocalizedStringName("UnitName_Paperclip"), GetLocalizedStringName("UnitAbbreviation_Paperclip"), 13, false, false, true ),
                new OrderedUnit( UnitConverterUnits.LengthHand, GetLocalizedStringName("UnitName_Hand"), GetLocalizedStringName("UnitAbbreviation_Hand"), 14, false, false, true ),
                new OrderedUnit( UnitConverterUnits.LengthJumboJet, GetLocalizedStringName("UnitName_JumboJet"), GetLocalizedStringName("UnitAbbreviation_JumboJet"), 15, false, false, true )
            };
            unitMap.Add(ViewMode.Length, lengthUnits);

            // --- Power Units ---
            var powerUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.PowerBritishThermalUnitPerMinute, GetLocalizedStringName("UnitName_BTUPerMinute"), GetLocalizedStringName("UnitAbbreviation_BTUPerMinute"), 5 ),
                new OrderedUnit( UnitConverterUnits.PowerFootPoundPerMinute, GetLocalizedStringName("UnitName_Foot-PoundPerMinute"), GetLocalizedStringName("UnitAbbreviation_Foot-PoundPerMinute"), 4 ),
                new OrderedUnit( UnitConverterUnits.PowerHorsepower, GetLocalizedStringName("UnitName_Horsepower"), GetLocalizedStringName("UnitAbbreviation_Horsepower"), 3, false, true, false ),
                new OrderedUnit( UnitConverterUnits.PowerKilowatt, GetLocalizedStringName("UnitName_Kilowatt"), GetLocalizedStringName("UnitAbbreviation_Kilowatt"), 2, !useWattInsteadOfKilowatt, false, false ),
                new OrderedUnit( UnitConverterUnits.PowerWatt, GetLocalizedStringName("UnitName_Watt"), GetLocalizedStringName("UnitAbbreviation_Watt"), 1, useWattInsteadOfKilowatt, false, false ),
                new OrderedUnit( UnitConverterUnits.PowerLightBulb, GetLocalizedStringName("UnitName_LightBulb"), GetLocalizedStringName("UnitAbbreviation_LightBulb"), 6, false, false, true ),
                new OrderedUnit( UnitConverterUnits.PowerHorse, GetLocalizedStringName("UnitName_Horse"), GetLocalizedStringName("UnitAbbreviation_Horse"), 7, false, false, true ),
                new OrderedUnit( UnitConverterUnits.PowerTrainEngine, GetLocalizedStringName("UnitName_TrainEngine"), GetLocalizedStringName("UnitAbbreviation_TrainEngine"), 8, false, false, true )
            };
            unitMap.Add(ViewMode.Power, powerUnits);

            // --- Temperature Units ---
            var tempUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.TemperatureDegreesCelsius, GetLocalizedStringName("UnitName_DegreesCelsius"), GetLocalizedStringName("UnitAbbreviation_DegreesCelsius"), 1, useFahrenheit, !useFahrenheit, false ),
                new OrderedUnit( UnitConverterUnits.TemperatureDegreesFahrenheit, GetLocalizedStringName("UnitName_DegreesFahrenheit"), GetLocalizedStringName("UnitAbbreviation_DegreesFahrenheit"), 2, !useFahrenheit, useFahrenheit, false ),
                new OrderedUnit( UnitConverterUnits.TemperatureKelvin, GetLocalizedStringName("UnitName_Kelvin"), GetLocalizedStringName("UnitAbbreviation_Kelvin"), 3 )
            };
            unitMap.Add(ViewMode.Temperature, tempUnits);

            // --- Time Units ---
            var timeUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.TimeDay, GetLocalizedStringName("UnitName_Day"), GetLocalizedStringName("UnitAbbreviation_Day"), 6 ),
                new OrderedUnit( UnitConverterUnits.TimeHour, GetLocalizedStringName("UnitName_Hour"), GetLocalizedStringName("UnitAbbreviation_Hour"), 5, true, false, false ),
                new OrderedUnit( UnitConverterUnits.TimeMicrosecond, GetLocalizedStringName("UnitName_Microsecond"), GetLocalizedStringName("UnitAbbreviation_Microsecond"), 1 ),
                new OrderedUnit( UnitConverterUnits.TimeMillisecond, GetLocalizedStringName("UnitName_Millisecond"), GetLocalizedStringName("UnitAbbreviation_Millisecond"), 2 ),
                new OrderedUnit( UnitConverterUnits.TimeMinute, GetLocalizedStringName("UnitName_Minute"), GetLocalizedStringName("UnitAbbreviation_Minute"), 4, false, true, false ),
                new OrderedUnit( UnitConverterUnits.TimeSecond, GetLocalizedStringName("UnitName_Second"), GetLocalizedStringName("UnitAbbreviation_Second"), 3 ),
                new OrderedUnit( UnitConverterUnits.TimeWeek, GetLocalizedStringName("UnitName_Week"), GetLocalizedStringName("UnitAbbreviation_Week"), 7 ),
                new OrderedUnit( UnitConverterUnits.TimeYear, GetLocalizedStringName("UnitName_Year"), GetLocalizedStringName("UnitAbbreviation_Year"), 8 )
            };
            unitMap.Add(ViewMode.Time, timeUnits);

            // --- Speed Units ---
            var speedUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.SpeedCentimetersPerSecond, GetLocalizedStringName("UnitName_CentimetersPerSecond"), GetLocalizedStringName("UnitAbbreviation_CentimetersPerSecond"), 1 ),
                new OrderedUnit( UnitConverterUnits.SpeedFeetPerSecond, GetLocalizedStringName("UnitName_FeetPerSecond"), GetLocalizedStringName("UnitAbbreviation_FeetPerSecond"), 4 ),
                new OrderedUnit( UnitConverterUnits.SpeedKilometersPerHour, GetLocalizedStringName("UnitName_KilometersPerHour"), GetLocalizedStringName("UnitAbbreviation_KilometersPerHour"), 3, useUSCustomary, useSI, false ),
                new OrderedUnit( UnitConverterUnits.SpeedKnot, GetLocalizedStringName("UnitName_Knot"), GetLocalizedStringName("UnitAbbreviation_Knot"), 6 ),
                new OrderedUnit( UnitConverterUnits.SpeedMach, GetLocalizedStringName("UnitName_Mach"), GetLocalizedStringName("UnitAbbreviation_Mach"), 7 ),
                new OrderedUnit( UnitConverterUnits.SpeedMetersPerSecond, GetLocalizedStringName("UnitName_MetersPerSecond"), GetLocalizedStringName("UnitAbbreviation_MetersPerSecond"), 2 ),
                new OrderedUnit( UnitConverterUnits.SpeedMilesPerHour, GetLocalizedStringName("UnitName_MilesPerHour"), GetLocalizedStringName("UnitAbbreviation_MilesPerHour"), 5, useSI, useUSCustomary, false ),
                new OrderedUnit( UnitConverterUnits.SpeedTurtle, GetLocalizedStringName("UnitName_Turtle"), GetLocalizedStringName("UnitAbbreviation_Turtle"), 8, false, false, true ),
                new OrderedUnit( UnitConverterUnits.SpeedHorse, GetLocalizedStringName("UnitName_Horse"), GetLocalizedStringName("UnitAbbreviation_Horse"), 9, false, false, true ),
                new OrderedUnit( UnitConverterUnits.SpeedJet, GetLocalizedStringName("UnitName_Jet"), GetLocalizedStringName("UnitAbbreviation_Jet"), 10, false, false, true )
            };
            unitMap.Add(ViewMode.Speed, speedUnits);

            // --- Volume Units ---
            var volumeUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.VolumeCubicCentimeter, GetLocalizedStringName("UnitName_CubicCentimeter"), GetLocalizedStringName("UnitAbbreviation_CubicCentimeter"), 2 ),
                new OrderedUnit( UnitConverterUnits.VolumeCubicFoot, GetLocalizedStringName("UnitName_CubicFoot"), GetLocalizedStringName("UnitAbbreviation_CubicFoot"), 13 ),
                new OrderedUnit( UnitConverterUnits.VolumeCubicInch, GetLocalizedStringName("UnitName_CubicInch"), GetLocalizedStringName("UnitAbbreviation_CubicInch"), 12 ),
                new OrderedUnit( UnitConverterUnits.VolumeCubicMeter, GetLocalizedStringName("UnitName_CubicMeter"), GetLocalizedStringName("UnitAbbreviation_CubicMeter"), 4 ),
                new OrderedUnit( UnitConverterUnits.VolumeCubicYard, GetLocalizedStringName("UnitName_CubicYard"), GetLocalizedStringName("UnitAbbreviation_CubicYard"), 14 ),
                new OrderedUnit( UnitConverterUnits.VolumeCupUS, GetLocalizedStringName("UnitName_CupUS"), GetLocalizedStringName("UnitAbbreviation_CupUS"), 8 ),
                new OrderedUnit( UnitConverterUnits.VolumeFluidOunceUK, GetLocalizedStringName("UnitName_FluidOunceUK"), GetLocalizedStringName("UnitAbbreviation_FluidOunceUK"), 17 ),
                new OrderedUnit( UnitConverterUnits.VolumeFluidOunceUS, GetLocalizedStringName("UnitName_FluidOunceUS"), GetLocalizedStringName("UnitAbbreviation_FluidOunceUS"), 7 ),
                new OrderedUnit( UnitConverterUnits.VolumeGallonUK, GetLocalizedStringName("UnitName_GallonUK"), GetLocalizedStringName("UnitAbbreviation_GallonUK"), 20 ),
                new OrderedUnit( UnitConverterUnits.VolumeGallonUS, GetLocalizedStringName("UnitName_GallonUS"), GetLocalizedStringName("UnitAbbreviation_GallonUS"), 11 ),
                new OrderedUnit( UnitConverterUnits.VolumeLiter, GetLocalizedStringName("UnitName_Liter"), GetLocalizedStringName("UnitAbbreviation_Liter"), 3 ),
                new OrderedUnit( UnitConverterUnits.VolumeMilliliter, GetLocalizedStringName("UnitName_Milliliter"), GetLocalizedStringName("UnitAbbreviation_Milliliter"), 1, useUSCustomary, useSI, false ),
                new OrderedUnit( UnitConverterUnits.VolumePintUK, GetLocalizedStringName("UnitName_PintUK"), GetLocalizedStringName("UnitAbbreviation_PintUK"), 18 ),
                new OrderedUnit( UnitConverterUnits.VolumePintUS, GetLocalizedStringName("UnitName_PintUS"), GetLocalizedStringName("UnitAbbreviation_PintUS"), 9 ),
                new OrderedUnit( UnitConverterUnits.VolumeTablespoonUS, GetLocalizedStringName("UnitName_TablespoonUS"), GetLocalizedStringName("UnitAbbreviation_TablespoonUS"), 6 ),
                new OrderedUnit( UnitConverterUnits.VolumeTeaspoonUS, GetLocalizedStringName("UnitName_TeaspoonUS"), GetLocalizedStringName("UnitAbbreviation_TeaspoonUS"), 5, useSI, useUSCustomary && m_currentRegionCode != "GB", false ),
                new OrderedUnit( UnitConverterUnits.VolumeQuartUK, GetLocalizedStringName("UnitName_QuartUK"), GetLocalizedStringName("UnitAbbreviation_QuartUK"), 19 ),
                new OrderedUnit( UnitConverterUnits.VolumeQuartUS, GetLocalizedStringName("UnitName_QuartUS"), GetLocalizedStringName("UnitAbbreviation_QuartUS"), 10 ),
                new OrderedUnit( UnitConverterUnits.VolumeTeaspoonUK, GetLocalizedStringName("UnitName_TeaspoonUK"), GetLocalizedStringName("UnitAbbreviation_TeaspoonUK"), 15, false, useUSCustomary && m_currentRegionCode == "GB", false ),
                new OrderedUnit( UnitConverterUnits.VolumeTablespoonUK, GetLocalizedStringName("UnitName_TablespoonUK"), GetLocalizedStringName("UnitAbbreviation_TablespoonUK"), 16 ),
                new OrderedUnit( UnitConverterUnits.VolumeCoffeeCup, GetLocalizedStringName("UnitName_CoffeeCup"), GetLocalizedStringName("UnitAbbreviation_CoffeeCup"), 22, false, false, true ),
                new OrderedUnit( UnitConverterUnits.VolumeBathtub, GetLocalizedStringName("UnitName_Bathtub"), GetLocalizedStringName("UnitAbbreviation_Bathtub"), 23, false, false, true ),
                new OrderedUnit( UnitConverterUnits.VolumeSwimmingPool, GetLocalizedStringName("UnitName_SwimmingPool"), GetLocalizedStringName("UnitAbbreviation_SwimmingPool"), 24, false, false, true )
            };
            unitMap.Add(ViewMode.Volume, volumeUnits);

            // --- Weight Units ---
            var weightUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.WeightCarat, GetLocalizedStringName("UnitName_Carat"), GetLocalizedStringName("UnitAbbreviation_Carat"), 1 ),
                new OrderedUnit( UnitConverterUnits.WeightCentigram, GetLocalizedStringName("UnitName_Centigram"), GetLocalizedStringName("UnitAbbreviation_Centigram"), 3 ),
                new OrderedUnit( UnitConverterUnits.WeightDecigram, GetLocalizedStringName("UnitName_Decigram"), GetLocalizedStringName("UnitAbbreviation_Decigram"), 4 ),
                new OrderedUnit( UnitConverterUnits.WeightDecagram, GetLocalizedStringName("UnitName_Decagram"), GetLocalizedStringName("UnitAbbreviation_Decagram"), 6 ),
                new OrderedUnit( UnitConverterUnits.WeightGram, GetLocalizedStringName("UnitName_Gram"), GetLocalizedStringName("UnitAbbreviation_Gram"), 5 ),
                new OrderedUnit( UnitConverterUnits.WeightHectogram, GetLocalizedStringName("UnitName_Hectogram"), GetLocalizedStringName("UnitAbbreviation_Hectogram"), 7 ),
                new OrderedUnit( UnitConverterUnits.WeightKilogram, GetLocalizedStringName("UnitName_Kilogram"), GetLocalizedStringName("UnitAbbreviation_Kilogram"), 8, useUSCustomary, useSI, false ),
                new OrderedUnit( UnitConverterUnits.WeightLongTon, GetLocalizedStringName("UnitName_LongTon"), GetLocalizedStringName("UnitAbbreviation_LongTon"), 14 ),
                new OrderedUnit( UnitConverterUnits.WeightMilligram, GetLocalizedStringName("UnitName_Milligram"), GetLocalizedStringName("UnitAbbreviation_Milligram"), 2 ),
                new OrderedUnit( UnitConverterUnits.WeightOunce, GetLocalizedStringName("UnitName_Ounce"), GetLocalizedStringName("UnitAbbreviation_Ounce"), 10 ),
                new OrderedUnit( UnitConverterUnits.WeightPound, GetLocalizedStringName("UnitName_Pound"), GetLocalizedStringName("UnitAbbreviation_Pound"), 11, useSI, useUSCustomary, false ),
                new OrderedUnit( UnitConverterUnits.WeightShortTon, GetLocalizedStringName("UnitName_ShortTon"), GetLocalizedStringName("UnitAbbreviation_ShortTon"), 13 ),
                new OrderedUnit( UnitConverterUnits.WeightStone, GetLocalizedStringName("UnitName_Stone"), GetLocalizedStringName("UnitAbbreviation_Stone"), 12 ),
                new OrderedUnit( UnitConverterUnits.WeightTonne, GetLocalizedStringName("UnitName_Tonne"), GetLocalizedStringName("UnitAbbreviation_Tonne"), 9 ),
                new OrderedUnit( UnitConverterUnits.WeightSnowflake, GetLocalizedStringName("UnitName_Snowflake"), GetLocalizedStringName("UnitAbbreviation_Snowflake"), 15, false, false, true ),
                new OrderedUnit( UnitConverterUnits.WeightSoccerBall, GetLocalizedStringName("UnitName_SoccerBall"), GetLocalizedStringName("UnitAbbreviation_SoccerBall"), 16, false, false, true ),
                new OrderedUnit( UnitConverterUnits.WeightElephant, GetLocalizedStringName("UnitName_Elephant"), GetLocalizedStringName("UnitAbbreviation_Elephant"), 17, false, false, true ),
                new OrderedUnit( UnitConverterUnits.WeightWhale, GetLocalizedStringName("UnitName_Whale"), GetLocalizedStringName("UnitAbbreviation_Whale"), 18, false, false, true )
            };
            unitMap.Add(ViewMode.Weight, weightUnits);

            // --- Pressure Units ---
            var pressureUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.PressureAtmosphere, GetLocalizedStringName("UnitName_Atmosphere"), GetLocalizedStringName("UnitAbbreviation_Atmosphere"), 1, true, false, false ),
                new OrderedUnit( UnitConverterUnits.PressureBar, GetLocalizedStringName("UnitName_Bar"), GetLocalizedStringName("UnitAbbreviation_Bar"), 2, false, true, false ),
                new OrderedUnit( UnitConverterUnits.PressureKiloPascal, GetLocalizedStringName("UnitName_KiloPascal"), GetLocalizedStringName("UnitAbbreviation_KiloPascal"), 3 ),
                new OrderedUnit( UnitConverterUnits.PressureMillimeterOfMercury, GetLocalizedStringName("UnitName_MillimeterOfMercury"), GetLocalizedStringName("UnitAbbreviation_MillimeterOfMercury"), 4 ),
                new OrderedUnit( UnitConverterUnits.PressurePascal, GetLocalizedStringName("UnitName_Pascal"), GetLocalizedStringName("UnitAbbreviation_Pascal"), 5 ),
                new OrderedUnit( UnitConverterUnits.PressurePSI, GetLocalizedStringName("UnitName_PSI"), GetLocalizedStringName("UnitAbbreviation_PSI"), 6, false, false, false )
            };
            unitMap.Add(ViewMode.Pressure, pressureUnits);

            // --- Angle Units ---
            var angleUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnits.AngleDegree, GetLocalizedStringName("UnitName_Degree"), GetLocalizedStringName("UnitAbbreviation_Degree"), 1, true, false, false ),
                new OrderedUnit( UnitConverterUnits.AngleRadian, GetLocalizedStringName("UnitName_Radian"), GetLocalizedStringName("UnitAbbreviation_Radian"), 2, false, true, false ),
                new OrderedUnit( UnitConverterUnits.AngleGradian, GetLocalizedStringName("UnitName_Gradian"), GetLocalizedStringName("UnitAbbreviation_Gradian"), 3 )
            };
            unitMap.Add(ViewMode.Angle, angleUnits);

            return unitMap;
        }

        private static Dictionary<ViewMode, Dictionary<int, string>> GetConversionData()
        {
            /*categoryId, UnitId, factor*/
            var unitDataList = new List<UnitData> {
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnits.AreaAcre, Factor = "4046.8564224" },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnits.AreaSquareMeter, Factor = "1" },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnits.AreaSquareFoot, Factor = "0.09290304" },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnits.AreaSquareYard, Factor = "0.83612736" },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnits.AreaSquareMillimeter, Factor = "0.000001" },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnits.AreaSquareCentimeter, Factor = "0.0001" },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnits.AreaSquareInch, Factor = "0.00064516" },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnits.AreaSquareMile, Factor = "2589988.110336" },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnits.AreaSquareKilometer, Factor = "1000000" },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnits.AreaHectare, Factor = "10000" },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnits.AreaHand, Factor = "0.012516104" },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnits.AreaPaper, Factor = "0.06032246" },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnits.AreaSoccerField, Factor = "10869.66" },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnits.AreaCastle, Factor = "100000" },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnits.AreaPyeong, Factor = "400.0 / 121.0" },

                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataBit, Factor = "0.000000125" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataNibble, Factor = "0.0000005" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataByte, Factor = "0.000001" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataKilobyte, Factor = "0.001" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataMegabyte, Factor = "1" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataGigabyte, Factor = "1000" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataTerabyte, Factor = "1000000" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataPetabyte, Factor = "1000000000" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataExabytes, Factor = "1000000000000" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataZetabytes, Factor = "1000000000000000" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataYottabyte, Factor = "1000000000000000000" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataKilobit, Factor = "0.000125" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataMegabit, Factor = "0.125" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataGigabit, Factor = "125" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataTerabit, Factor = "125000" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataPetabit, Factor = "125000000" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataExabits, Factor = "125000000000" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataZetabits, Factor = "125000000000000" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataYottabit, Factor = "125000000000000000" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataGibibits, Factor = "134.217728" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataGibibytes, Factor = "1073.741824" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataKibibits, Factor = "0.000128" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataKibibytes, Factor = "0.001024" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataMebibits, Factor = "0.131072" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataMebibytes, Factor = "1.048576" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataPebibits, Factor = "140737488.355328" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataPebibytes, Factor = "1125899906.842624" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataTebibits, Factor = "137438.953472" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataTebibytes, Factor = "1099511.627776" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataExbibits, Factor = "144115188075.855872" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataExbibytes, Factor = "1152921504606.846976" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataZebibits, Factor = "147573952589676.412928" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataZebibytes, Factor = "1180591620717411.303424" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataYobibits, Factor = "151115727451828646.838272" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataYobibytes, Factor = "1208925819614629174.706176" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataFloppyDisk, Factor = "1.474560" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataCD, Factor = "700" },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnits.DataDVD, Factor = "4700" },

                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnits.EnergyCalorie, Factor = "4.184" },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnits.EnergyKilocalorie, Factor = "4184" },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnits.EnergyBritishThermalUnit, Factor = "1055.056" },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnits.EnergyKilojoule, Factor = "1000" },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnits.EnergyKilowatthour, Factor = "3600000" },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnits.EnergyElectronVolt, Factor = "0.0000000000000000001602176565" },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnits.EnergyJoule, Factor = "1" },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnits.EnergyFootPound, Factor = "1.3558179483314" },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnits.EnergyBattery, Factor = "9000" },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnits.EnergyBanana, Factor = "439614" },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnits.EnergySliceOfCake, Factor = "1046700" },

                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnits.LengthInch, Factor = "0.0254" },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnits.LengthFoot, Factor = "0.3048" },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnits.LengthYard, Factor = "0.9144" },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnits.LengthMile, Factor = "1609.344" },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnits.LengthMicron, Factor = "0.000001" },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnits.LengthMillimeter, Factor = "0.001" },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnits.LengthNanometer, Factor = "0.000000001" },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnits.LengthAngstrom, Factor = "0.0000000001" },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnits.LengthCentimeter, Factor = "0.01" },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnits.LengthMeter, Factor = "1" },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnits.LengthKilometer, Factor = "1000" },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnits.LengthNauticalMile, Factor = "1852" },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnits.LengthPaperclip, Factor = "0.035052" },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnits.LengthHand, Factor = "0.18669" },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnits.LengthJumboJet, Factor = "76" },

                new UnitData { CategoryId = ViewMode.Power, UnitId = UnitConverterUnits.PowerBritishThermalUnitPerMinute, Factor = "17.58426666666667" },
                new UnitData { CategoryId = ViewMode.Power, UnitId = UnitConverterUnits.PowerFootPoundPerMinute, Factor = "0.0225969658055233" },
                new UnitData { CategoryId = ViewMode.Power, UnitId = UnitConverterUnits.PowerWatt, Factor = "1" },
                new UnitData { CategoryId = ViewMode.Power, UnitId = UnitConverterUnits.PowerKilowatt, Factor = "1000" },
                new UnitData { CategoryId = ViewMode.Power, UnitId = UnitConverterUnits.PowerHorsepower, Factor = "745.69987158227022" },
                new UnitData { CategoryId = ViewMode.Power, UnitId = UnitConverterUnits.PowerLightBulb, Factor = "60" },
                new UnitData { CategoryId = ViewMode.Power, UnitId = UnitConverterUnits.PowerHorse, Factor = "745.7" },
                new UnitData { CategoryId = ViewMode.Power, UnitId = UnitConverterUnits.PowerTrainEngine, Factor = "2982799.486329081" },

                new UnitData { CategoryId = ViewMode.Time, UnitId = UnitConverterUnits.TimeDay, Factor = "86400" },
                new UnitData { CategoryId = ViewMode.Time, UnitId = UnitConverterUnits.TimeSecond, Factor = "1" },
                new UnitData { CategoryId = ViewMode.Time, UnitId = UnitConverterUnits.TimeWeek, Factor = "604800" },
                new UnitData { CategoryId = ViewMode.Time, UnitId = UnitConverterUnits.TimeYear, Factor = "31557600" },
                new UnitData { CategoryId = ViewMode.Time, UnitId = UnitConverterUnits.TimeMillisecond, Factor = "0.001" },
                new UnitData { CategoryId = ViewMode.Time, UnitId = UnitConverterUnits.TimeMicrosecond, Factor = "0.000001" },
                new UnitData { CategoryId = ViewMode.Time, UnitId = UnitConverterUnits.TimeMinute, Factor = "60" },
                new UnitData { CategoryId = ViewMode.Time, UnitId = UnitConverterUnits.TimeHour, Factor = "3600" },

                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeCupUS, Factor = "236.588237" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumePintUS, Factor = "473.176473" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumePintUK, Factor = "568.26125" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeQuartUS, Factor = "946.352946" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeQuartUK, Factor = "1136.5225" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeGallonUS, Factor = "3785.411784" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeGallonUK, Factor = "4546.09" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeLiter, Factor = "1000" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeTeaspoonUS, Factor = "4.92892159375" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeTablespoonUS, Factor = "14.78676478125" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeCubicCentimeter, Factor = "1" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeCubicYard, Factor = "764554.857984" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeCubicMeter, Factor = "1000000" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeMilliliter, Factor = "1" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeCubicInch, Factor = "16.387064" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeCubicFoot, Factor = "28316.846592" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeFluidOunceUS, Factor = "29.5735295625" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeFluidOunceUK, Factor = "28.4130625" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeTeaspoonUK, Factor = "5.91938802083333333333" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeTablespoonUK, Factor = "17.7581640625" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeCoffeeCup, Factor = "236.5882" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeBathtub, Factor = "378541.2" },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnits.VolumeSwimmingPool, Factor = "3750000000" },

                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightKilogram, Factor = "1" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightHectogram, Factor = "0.1" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightDecagram, Factor = "0.01" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightGram, Factor = "0.001" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightPound, Factor = "0.45359237" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightOunce, Factor = "0.028349523125" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightMilligram, Factor = "0.000001" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightCentigram, Factor = "0.00001" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightDecigram, Factor = "0.0001" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightLongTon, Factor = "1016.0469088" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightTonne, Factor = "1000" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightStone, Factor = "6.35029318" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightCarat, Factor = "0.0002" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightShortTon, Factor = "907.18474" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightSnowflake, Factor = "0.000002" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightSoccerBall, Factor = "0.4325" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightElephant, Factor = "4000" },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnits.WeightWhale, Factor = "90000" },

                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnits.SpeedCentimetersPerSecond, Factor = "1" },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnits.SpeedFeetPerSecond, Factor = "30.48" },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnits.SpeedKilometersPerHour, Factor = "27.777777777777777777778" },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnits.SpeedKnot, Factor = "51.44" },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnits.SpeedMach, Factor = "34030" },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnits.SpeedMetersPerSecond, Factor = "100" },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnits.SpeedMilesPerHour, Factor = "44.7" },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnits.SpeedTurtle, Factor = "8.94" },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnits.SpeedHorse, Factor = "2011.5" },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnits.SpeedJet, Factor = "24585" },

                new UnitData { CategoryId = ViewMode.Angle, UnitId = UnitConverterUnits.AngleDegree, Factor = "1" },
                new UnitData { CategoryId = ViewMode.Angle, UnitId = UnitConverterUnits.AngleRadian, Factor = "57.29577951308233" },
                new UnitData { CategoryId = ViewMode.Angle, UnitId = UnitConverterUnits.AngleGradian, Factor = "0.9" },

                new UnitData { CategoryId = ViewMode.Pressure, UnitId = UnitConverterUnits.PressureAtmosphere, Factor = "1" },
                new UnitData { CategoryId = ViewMode.Pressure, UnitId = UnitConverterUnits.PressureBar, Factor = "0.9869232667160128" },
                new UnitData { CategoryId = ViewMode.Pressure, UnitId = UnitConverterUnits.PressureKiloPascal, Factor = "0.0098692326671601" },
                new UnitData { CategoryId = ViewMode.Pressure, UnitId = UnitConverterUnits.PressureMillimeterOfMercury, Factor = "0.0013155687145324" },
                new UnitData { CategoryId = ViewMode.Pressure, UnitId = UnitConverterUnits.PressurePascal, Factor = "9.869232667160128e-6" },
                new UnitData { CategoryId = ViewMode.Pressure, UnitId = UnitConverterUnits.PressurePSI, Factor = "0.068045961016531" }
            };

            var categoryToUnitConversionMap = new Dictionary<ViewMode, Dictionary<int, string>>();

            // Populate the hash map and return;
            foreach (UnitData unitdata in unitDataList)
            {
                if (!categoryToUnitConversionMap.TryGetValue(unitdata.CategoryId, out Dictionary<int, string>? conversionData))
                {
                    conversionData = new Dictionary<int, string>();
                    categoryToUnitConversionMap.Add(unitdata.CategoryId, conversionData);
                }
                conversionData.Add((int)unitdata.UnitId, unitdata.Factor);
            }
            return categoryToUnitConversionMap;
        }

        private static Dictionary<int, Dictionary<int, CalcManager.ConversionData>> GetExplicitConversionData()
        {
            /* categoryId, ParentUnitId, UnitId, ratio, offset, offsetfirst*/
            var conversionDataList = new ExplicitUnitConversionData[] {
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.TemperatureDegreesCelsius, UnitConverterUnits.TemperatureDegreesCelsius, "1", "0" ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.TemperatureDegreesCelsius, UnitConverterUnits.TemperatureDegreesFahrenheit, "1.8", "32" ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.TemperatureDegreesCelsius, UnitConverterUnits.TemperatureKelvin, "1", "273.15" ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.TemperatureDegreesFahrenheit, UnitConverterUnits.TemperatureDegreesCelsius, "0.55555555555555555555555555555556", "-32", CONVERT_WITH_OFFSET_FIRST ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.TemperatureDegreesFahrenheit, UnitConverterUnits.TemperatureDegreesFahrenheit, "1", "0" ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.TemperatureDegreesFahrenheit, UnitConverterUnits.TemperatureKelvin, "0.55555555555555555555555555555556", "459.67", CONVERT_WITH_OFFSET_FIRST ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.TemperatureKelvin, UnitConverterUnits.TemperatureDegreesCelsius, "1", "-273.15", CONVERT_WITH_OFFSET_FIRST ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.TemperatureKelvin, UnitConverterUnits.TemperatureDegreesFahrenheit, "1.8", "-459.67" ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnits.TemperatureKelvin, UnitConverterUnits.TemperatureKelvin, "1", "0" )
            };

            var unitToUnitConversionList = new Dictionary<int, Dictionary<int, CalcManager.ConversionData>>();

            // Populate the hash map and return;
            foreach (ExplicitUnitConversionData data in conversionDataList)
            {
                if (!unitToUnitConversionList.TryGetValue((int)data.ParentUnitId, out Dictionary<int, CalcManager.ConversionData>? conversionData))
                {
                    conversionData = new Dictionary<int, CalcManager.ConversionData>();
                    unitToUnitConversionList.Add((int)data.ParentUnitId, conversionData);
                }
                conversionData.Add((int)data.UnitId, data);
            }
            return unitToUnitConversionList;
        }

        private static string GetLocalizedStringName(string stringId)
        {
            return AppResourceProvider.Instance.GetResourceString(stringId);
        }
    }
}
