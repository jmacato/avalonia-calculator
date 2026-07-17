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
            if (category is null)
            {
                throw new ArgumentNullException(nameof(category));
            }

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

                if (!orderedUnitMap.TryGetValue(categoryViewMode, out List<OrderedUnit> orderedUnits))
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

                    if (!explicitConversionData.TryGetValue(unit.Id, out Dictionary<int, CalcManager.ConversionData> explicitUnitConversions))
                    {
                        // Get the associated units for a category id
                        if (!categoryToUnitConversionDataMap.TryGetValue(categoryViewMode, out Dictionary<int, double> unitConversions))
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

                            var parsedData = new CalcManager.ConversionData { Ratio = 1.0, Offset = "0", OffsetFirst = false };
                            Debug.Assert(conversionFactor > 0); // divide by zero assert
                            parsedData.Ratio = unitFactor / conversionFactor;
                            conversions.Add(idToUnit[id], parsedData);
                        }
                    }
                    else
                    {
                        foreach (var kvp in explicitUnitConversions)
                        {
                            if (idToUnit.TryGetValue(kvp.Key, out OrderedUnit targetUnit))
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
                new OrderedUnit( UnitConverterUnit.AreaAcre, GetLocalizedStringName("UnitName_Acre"), GetLocalizedStringName("UnitAbbreviation_Acre"), 9 ),
                new OrderedUnit( UnitConverterUnit.AreaHectare, GetLocalizedStringName("UnitName_Hectare"), GetLocalizedStringName("UnitAbbreviation_Hectare"), 4 ),
                new OrderedUnit( UnitConverterUnit.AreaSquareCentimeter, GetLocalizedStringName("UnitName_SquareCentimeter"), GetLocalizedStringName("UnitAbbreviation_SquareCentimeter"), 2 ),
                new OrderedUnit( UnitConverterUnit.AreaSquareFoot, GetLocalizedStringName("UnitName_SquareFoot"), GetLocalizedStringName("UnitAbbreviation_SquareFoot"), 7, useSI, useUSCustomary, false ),
                new OrderedUnit( UnitConverterUnit.AreaSquareInch, GetLocalizedStringName("UnitName_SquareInch"), GetLocalizedStringName("UnitAbbreviation_SquareInch"), 6 ),
                new OrderedUnit( UnitConverterUnit.AreaSquareKilometer, GetLocalizedStringName("UnitName_SquareKilometer"), GetLocalizedStringName("UnitAbbreviation_SquareKilometer"), 5 ),
                new OrderedUnit( UnitConverterUnit.AreaSquareMeter, GetLocalizedStringName("UnitName_SquareMeter"), GetLocalizedStringName("UnitAbbreviation_SquareMeter"), 3, useUSCustomary, useSI, false ),
                new OrderedUnit( UnitConverterUnit.AreaSquareMile, GetLocalizedStringName("UnitName_SquareMile"), GetLocalizedStringName("UnitAbbreviation_SquareMile"), 10 ),
                new OrderedUnit( UnitConverterUnit.AreaSquareMillimeter, GetLocalizedStringName("UnitName_SquareMillimeter"), GetLocalizedStringName("UnitAbbreviation_SquareMillimeter"), 1 ),
                new OrderedUnit( UnitConverterUnit.AreaSquareYard, GetLocalizedStringName("UnitName_SquareYard"), GetLocalizedStringName("UnitAbbreviation_SquareYard"), 8 ),
                new OrderedUnit( UnitConverterUnit.AreaHand, GetLocalizedStringName("UnitName_Hand"), GetLocalizedStringName("UnitAbbreviation_Hand"), 11, false, false, true ),
                new OrderedUnit( UnitConverterUnit.AreaPaper, GetLocalizedStringName("UnitName_Paper"), GetLocalizedStringName("UnitAbbreviation_Paper"), 12, false, false, true ),
                new OrderedUnit( UnitConverterUnit.AreaSoccerField, GetLocalizedStringName("UnitName_SoccerField"), GetLocalizedStringName("UnitAbbreviation_SoccerField"), 13, false, false, true ),
                new OrderedUnit( UnitConverterUnit.AreaCastle, GetLocalizedStringName("UnitName_Castle"), GetLocalizedStringName("UnitAbbreviation_Castle"), 14, false, false, true )
            };
            if (usePyeong)
            {
                areaUnits.Add(new OrderedUnit(UnitConverterUnit.AreaPyeong, GetLocalizedStringName("UnitName_Pyeong"), GetLocalizedStringName("UnitAbbreviation_Pyeong"), 15, false, false, false));
            }
            unitMap.Add(ViewMode.Area, areaUnits);

            // --- Data Units ---
            var dataUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnit.DataBit, GetLocalizedStringName("UnitName_Bit"), GetLocalizedStringName("UnitAbbreviation_Bit"), 1 ),
                new OrderedUnit( UnitConverterUnit.DataByte, GetLocalizedStringName("UnitName_Byte"), GetLocalizedStringName("UnitAbbreviation_Byte"), 3 ),
                new OrderedUnit( UnitConverterUnit.DataExabits, GetLocalizedStringName("UnitName_Exabits"), GetLocalizedStringName("UnitAbbreviation_Exabits"), 24 ),
                new OrderedUnit( UnitConverterUnit.DataExabytes, GetLocalizedStringName("UnitName_Exabytes"), GetLocalizedStringName("UnitAbbreviation_Exabytes"), 26 ),
                new OrderedUnit( UnitConverterUnit.DataExbibits, GetLocalizedStringName("UnitName_Exbibits"), GetLocalizedStringName("UnitAbbreviation_Exbibits"), 25 ),
                new OrderedUnit( UnitConverterUnit.DataExbibytes, GetLocalizedStringName("UnitName_Exbibytes"), GetLocalizedStringName("UnitAbbreviation_Exbibytes"), 27 ),
                new OrderedUnit( UnitConverterUnit.DataGibibits, GetLocalizedStringName("UnitName_Gibibits"), GetLocalizedStringName("UnitAbbreviation_Gibibits"), 13 ),
                new OrderedUnit( UnitConverterUnit.DataGibibytes, GetLocalizedStringName("UnitName_Gibibytes"), GetLocalizedStringName("UnitAbbreviation_Gibibytes"), 15 ),
                new OrderedUnit( UnitConverterUnit.DataGigabit, GetLocalizedStringName("UnitName_Gigabit"), GetLocalizedStringName("UnitAbbreviation_Gigabit"), 12 ),
                new OrderedUnit( UnitConverterUnit.DataGigabyte, GetLocalizedStringName("UnitName_Gigabyte"), GetLocalizedStringName("UnitAbbreviation_Gigabyte"), 14, true, false, false ),
                new OrderedUnit( UnitConverterUnit.DataKibibits, GetLocalizedStringName("UnitName_Kibibits"), GetLocalizedStringName("UnitAbbreviation_Kibibits"), 5 ),
                new OrderedUnit( UnitConverterUnit.DataKibibytes, GetLocalizedStringName("UnitName_Kibibytes"), GetLocalizedStringName("UnitAbbreviation_Kibibytes"), 7 ),
                new OrderedUnit( UnitConverterUnit.DataKilobit, GetLocalizedStringName("UnitName_Kilobit"), GetLocalizedStringName("UnitAbbreviation_Kilobit"), 4 ),
                new OrderedUnit( UnitConverterUnit.DataKilobyte, GetLocalizedStringName("UnitName_Kilobyte"), GetLocalizedStringName("UnitAbbreviation_Kilobyte"), 6 ),
                new OrderedUnit( UnitConverterUnit.DataMebibits, GetLocalizedStringName("UnitName_Mebibits"), GetLocalizedStringName("UnitAbbreviation_Mebibits"), 9 ),
                new OrderedUnit( UnitConverterUnit.DataMebibytes, GetLocalizedStringName("UnitName_Mebibytes"), GetLocalizedStringName("UnitAbbreviation_Mebibytes"), 11 ),
                new OrderedUnit( UnitConverterUnit.DataMegabit, GetLocalizedStringName("UnitName_Megabit"), GetLocalizedStringName("UnitAbbreviation_Megabit"), 8 ),
                new OrderedUnit( UnitConverterUnit.DataMegabyte, GetLocalizedStringName("UnitName_Megabyte"), GetLocalizedStringName("UnitAbbreviation_Megabyte"), 10, false, true, false ),
                new OrderedUnit( UnitConverterUnit.DataNibble, GetLocalizedStringName("UnitName_Nibble"), GetLocalizedStringName("UnitAbbreviation_Nibble"), 2 ),
                new OrderedUnit( UnitConverterUnit.DataPebibits, GetLocalizedStringName("UnitName_Pebibits"), GetLocalizedStringName("UnitAbbreviation_Pebibits"), 21 ),
                new OrderedUnit( UnitConverterUnit.DataPebibytes, GetLocalizedStringName("UnitName_Pebibytes"), GetLocalizedStringName("UnitAbbreviation_Pebibytes"), 23 ),
                new OrderedUnit( UnitConverterUnit.DataPetabit, GetLocalizedStringName("UnitName_Petabit"), GetLocalizedStringName("UnitAbbreviation_Petabit"), 20 ),
                new OrderedUnit( UnitConverterUnit.DataPetabyte, GetLocalizedStringName("UnitName_Petabyte"), GetLocalizedStringName("UnitAbbreviation_Petabyte"), 22 ),
                new OrderedUnit( UnitConverterUnit.DataTebibits, GetLocalizedStringName("UnitName_Tebibits"), GetLocalizedStringName("UnitAbbreviation_Tebibits"), 17 ),
                new OrderedUnit( UnitConverterUnit.DataTebibytes, GetLocalizedStringName("UnitName_Tebibytes"), GetLocalizedStringName("UnitAbbreviation_Tebibytes"), 19 ),
                new OrderedUnit( UnitConverterUnit.DataTerabit, GetLocalizedStringName("UnitName_Terabit"), GetLocalizedStringName("UnitAbbreviation_Terabit"), 16 ),
                new OrderedUnit( UnitConverterUnit.DataTerabyte, GetLocalizedStringName("UnitName_Terabyte"), GetLocalizedStringName("UnitAbbreviation_Terabyte"), 18 ),
                new OrderedUnit( UnitConverterUnit.DataYobibits, GetLocalizedStringName("UnitName_Yobibits"), GetLocalizedStringName("UnitAbbreviation_Yobibits"), 33 ),
                new OrderedUnit( UnitConverterUnit.DataYobibytes, GetLocalizedStringName("UnitName_Yobibytes"), GetLocalizedStringName("UnitAbbreviation_Yobibytes"), 35 ),
                new OrderedUnit( UnitConverterUnit.DataYottabit, GetLocalizedStringName("UnitName_Yottabit"), GetLocalizedStringName("UnitAbbreviation_Yottabit"), 32 ),
                new OrderedUnit( UnitConverterUnit.DataYottabyte, GetLocalizedStringName("UnitName_Yottabyte"), GetLocalizedStringName("UnitAbbreviation_Yottabyte"), 34 ),
                new OrderedUnit( UnitConverterUnit.DataZebibits, GetLocalizedStringName("UnitName_Zebibits"), GetLocalizedStringName("UnitAbbreviation_Zebibits"), 29 ),
                new OrderedUnit( UnitConverterUnit.DataZebibytes, GetLocalizedStringName("UnitName_Zebibytes"), GetLocalizedStringName("UnitAbbreviation_Zebibytes"), 31 ),
                new OrderedUnit( UnitConverterUnit.DataZetabits, GetLocalizedStringName("UnitName_Zetabits"), GetLocalizedStringName("UnitAbbreviation_Zetabits"), 28 ),
                new OrderedUnit( UnitConverterUnit.DataZetabytes, GetLocalizedStringName("UnitName_Zetabytes"), GetLocalizedStringName("UnitAbbreviation_Zetabytes"), 30 ),
                new OrderedUnit( UnitConverterUnit.DataFloppyDisk, GetLocalizedStringName("UnitName_FloppyDisk"), GetLocalizedStringName("UnitAbbreviation_FloppyDisk"), 13, false, false, true ),
                new OrderedUnit( UnitConverterUnit.DataCD, GetLocalizedStringName("UnitName_CD"), GetLocalizedStringName("UnitAbbreviation_CD"), 14, false, false, true ),
                new OrderedUnit( UnitConverterUnit.DataDVD, GetLocalizedStringName("UnitName_DVD"), GetLocalizedStringName("UnitAbbreviation_DVD"), 15, false, false, true )
            };
            unitMap.Add(ViewMode.Data, dataUnits);

            // --- Energy Units ---
            var energyUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnit.EnergyBritishThermalUnit, GetLocalizedStringName("UnitName_BritishThermalUnit"), GetLocalizedStringName("UnitAbbreviation_BritishThermalUnit"), 7 ),
                new OrderedUnit( UnitConverterUnit.EnergyCalorie, GetLocalizedStringName("UnitName_Calorie"), GetLocalizedStringName("UnitAbbreviation_Calorie"), 4 ),
                new OrderedUnit( UnitConverterUnit.EnergyElectronVolt, GetLocalizedStringName("UnitName_Electron-Volt"), GetLocalizedStringName("UnitAbbreviation_Electron-Volt"), 1 ),
                new OrderedUnit( UnitConverterUnit.EnergyFootPound, GetLocalizedStringName("UnitName_Foot-Pound"), GetLocalizedStringName("UnitAbbreviation_Foot-Pound"), 6 ),
                new OrderedUnit( UnitConverterUnit.EnergyJoule, GetLocalizedStringName("UnitName_Joule"), GetLocalizedStringName("UnitAbbreviation_Joule"), 2, true, false, false ),
                new OrderedUnit( UnitConverterUnit.EnergyKilowatthour, GetLocalizedStringName("UnitName_Kilowatthour"), GetLocalizedStringName("UnitAbbreviation_Kilowatthour"), 166, true, false, false ),
                new OrderedUnit( UnitConverterUnit.EnergyKilocalorie, GetLocalizedStringName("UnitName_Kilocalorie"), GetLocalizedStringName("UnitAbbreviation_Kilocalorie"), 5, false, true, false ),
                new OrderedUnit( UnitConverterUnit.EnergyKilojoule, GetLocalizedStringName("UnitName_Kilojoule"), GetLocalizedStringName("UnitAbbreviation_Kilojoule"), 3 ),
                new OrderedUnit( UnitConverterUnit.EnergyBattery, GetLocalizedStringName("UnitName_Battery"), GetLocalizedStringName("UnitAbbreviation_Battery"), 8, false, false, true ),
                new OrderedUnit( UnitConverterUnit.EnergyBanana, GetLocalizedStringName("UnitName_Banana"), GetLocalizedStringName("UnitAbbreviation_Banana"), 9, false, false, true ),
                new OrderedUnit( UnitConverterUnit.EnergySliceOfCake, GetLocalizedStringName("UnitName_SliceOfCake"), GetLocalizedStringName("UnitAbbreviation_SliceOfCake"), 10, false, false, true )
            };
            unitMap.Add(ViewMode.Energy, energyUnits);

            // --- Length Units ---
            var lengthUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnit.LengthAngstrom, GetLocalizedStringName("UnitName_Angstrom"), GetLocalizedStringName("UnitAbbreviation_Angstrom"), 1 ),
                new OrderedUnit( UnitConverterUnit.LengthCentimeter, GetLocalizedStringName("UnitName_Centimeter"), GetLocalizedStringName("UnitAbbreviation_Centimeter"), 5, useUSCustomary, useSI, false ),
                new OrderedUnit( UnitConverterUnit.LengthFoot, GetLocalizedStringName("UnitName_Foot"), GetLocalizedStringName("UnitAbbreviation_Foot"), 9 ),
                new OrderedUnit( UnitConverterUnit.LengthInch, GetLocalizedStringName("UnitName_Inch"), GetLocalizedStringName("UnitAbbreviation_Inch"), 8, useSI, useUSCustomary, false ),
                new OrderedUnit( UnitConverterUnit.LengthKilometer, GetLocalizedStringName("UnitName_Kilometer"), GetLocalizedStringName("UnitAbbreviation_Kilometer"), 7 ),
                new OrderedUnit( UnitConverterUnit.LengthMeter, GetLocalizedStringName("UnitName_Meter"), GetLocalizedStringName("UnitAbbreviation_Meter"), 6 ),
                new OrderedUnit( UnitConverterUnit.LengthMicron, GetLocalizedStringName("UnitName_Micron"), GetLocalizedStringName("UnitAbbreviation_Micron"), 3 ),
                new OrderedUnit( UnitConverterUnit.LengthMile, GetLocalizedStringName("UnitName_Mile"), GetLocalizedStringName("UnitAbbreviation_Mile"), 11 ),
                new OrderedUnit( UnitConverterUnit.LengthMillimeter, GetLocalizedStringName("UnitName_Millimeter"), GetLocalizedStringName("UnitAbbreviation_Millimeter"), 4 ),
                new OrderedUnit( UnitConverterUnit.LengthNanometer, GetLocalizedStringName("UnitName_Nanometer"), GetLocalizedStringName("UnitAbbreviation_Nanometer"), 2 ),
                new OrderedUnit( UnitConverterUnit.LengthNauticalMile, GetLocalizedStringName("UnitName_NauticalMile"), GetLocalizedStringName("UnitAbbreviation_NauticalMile"), 12 ),
                new OrderedUnit( UnitConverterUnit.LengthYard, GetLocalizedStringName("UnitName_Yard"), GetLocalizedStringName("UnitAbbreviation_Yard"), 10 ),
                new OrderedUnit( UnitConverterUnit.LengthPaperclip, GetLocalizedStringName("UnitName_Paperclip"), GetLocalizedStringName("UnitAbbreviation_Paperclip"), 13, false, false, true ),
                new OrderedUnit( UnitConverterUnit.LengthHand, GetLocalizedStringName("UnitName_Hand"), GetLocalizedStringName("UnitAbbreviation_Hand"), 14, false, false, true ),
                new OrderedUnit( UnitConverterUnit.LengthJumboJet, GetLocalizedStringName("UnitName_JumboJet"), GetLocalizedStringName("UnitAbbreviation_JumboJet"), 15, false, false, true )
            };
            unitMap.Add(ViewMode.Length, lengthUnits);

            // --- Power Units ---
            var powerUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnit.PowerBritishThermalUnitPerMinute, GetLocalizedStringName("UnitName_BTUPerMinute"), GetLocalizedStringName("UnitAbbreviation_BTUPerMinute"), 5 ),
                new OrderedUnit( UnitConverterUnit.PowerFootPoundPerMinute, GetLocalizedStringName("UnitName_Foot-PoundPerMinute"), GetLocalizedStringName("UnitAbbreviation_Foot-PoundPerMinute"), 4 ),
                new OrderedUnit( UnitConverterUnit.PowerHorsepower, GetLocalizedStringName("UnitName_Horsepower"), GetLocalizedStringName("UnitAbbreviation_Horsepower"), 3, false, true, false ),
                new OrderedUnit( UnitConverterUnit.PowerKilowatt, GetLocalizedStringName("UnitName_Kilowatt"), GetLocalizedStringName("UnitAbbreviation_Kilowatt"), 2, !useWattInsteadOfKilowatt, false, false ),
                new OrderedUnit( UnitConverterUnit.PowerWatt, GetLocalizedStringName("UnitName_Watt"), GetLocalizedStringName("UnitAbbreviation_Watt"), 1, useWattInsteadOfKilowatt, false, false ),
                new OrderedUnit( UnitConverterUnit.PowerLightBulb, GetLocalizedStringName("UnitName_LightBulb"), GetLocalizedStringName("UnitAbbreviation_LightBulb"), 6, false, false, true ),
                new OrderedUnit( UnitConverterUnit.PowerHorse, GetLocalizedStringName("UnitName_Horse"), GetLocalizedStringName("UnitAbbreviation_Horse"), 7, false, false, true ),
                new OrderedUnit( UnitConverterUnit.PowerTrainEngine, GetLocalizedStringName("UnitName_TrainEngine"), GetLocalizedStringName("UnitAbbreviation_TrainEngine"), 8, false, false, true )
            };
            unitMap.Add(ViewMode.Power, powerUnits);

            // --- Temperature Units ---
            var tempUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnit.TemperatureDegreesCelsius, GetLocalizedStringName("UnitName_DegreesCelsius"), GetLocalizedStringName("UnitAbbreviation_DegreesCelsius"), 1, useFahrenheit, !useFahrenheit, false ),
                new OrderedUnit( UnitConverterUnit.TemperatureDegreesFahrenheit, GetLocalizedStringName("UnitName_DegreesFahrenheit"), GetLocalizedStringName("UnitAbbreviation_DegreesFahrenheit"), 2, !useFahrenheit, useFahrenheit, false ),
                new OrderedUnit( UnitConverterUnit.TemperatureKelvin, GetLocalizedStringName("UnitName_Kelvin"), GetLocalizedStringName("UnitAbbreviation_Kelvin"), 3 )
            };
            unitMap.Add(ViewMode.Temperature, tempUnits);

            // --- Time Units ---
            var timeUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnit.TimeDay, GetLocalizedStringName("UnitName_Day"), GetLocalizedStringName("UnitAbbreviation_Day"), 6 ),
                new OrderedUnit( UnitConverterUnit.TimeHour, GetLocalizedStringName("UnitName_Hour"), GetLocalizedStringName("UnitAbbreviation_Hour"), 5, true, false, false ),
                new OrderedUnit( UnitConverterUnit.TimeMicrosecond, GetLocalizedStringName("UnitName_Microsecond"), GetLocalizedStringName("UnitAbbreviation_Microsecond"), 1 ),
                new OrderedUnit( UnitConverterUnit.TimeMillisecond, GetLocalizedStringName("UnitName_Millisecond"), GetLocalizedStringName("UnitAbbreviation_Millisecond"), 2 ),
                new OrderedUnit( UnitConverterUnit.TimeMinute, GetLocalizedStringName("UnitName_Minute"), GetLocalizedStringName("UnitAbbreviation_Minute"), 4, false, true, false ),
                new OrderedUnit( UnitConverterUnit.TimeSecond, GetLocalizedStringName("UnitName_Second"), GetLocalizedStringName("UnitAbbreviation_Second"), 3 ),
                new OrderedUnit( UnitConverterUnit.TimeWeek, GetLocalizedStringName("UnitName_Week"), GetLocalizedStringName("UnitAbbreviation_Week"), 7 ),
                new OrderedUnit( UnitConverterUnit.TimeYear, GetLocalizedStringName("UnitName_Year"), GetLocalizedStringName("UnitAbbreviation_Year"), 8 )
            };
            unitMap.Add(ViewMode.Time, timeUnits);

            // --- Speed Units ---
            var speedUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnit.SpeedCentimetersPerSecond, GetLocalizedStringName("UnitName_CentimetersPerSecond"), GetLocalizedStringName("UnitAbbreviation_CentimetersPerSecond"), 1 ),
                new OrderedUnit( UnitConverterUnit.SpeedFeetPerSecond, GetLocalizedStringName("UnitName_FeetPerSecond"), GetLocalizedStringName("UnitAbbreviation_FeetPerSecond"), 4 ),
                new OrderedUnit( UnitConverterUnit.SpeedKilometersPerHour, GetLocalizedStringName("UnitName_KilometersPerHour"), GetLocalizedStringName("UnitAbbreviation_KilometersPerHour"), 3, useUSCustomary, useSI, false ),
                new OrderedUnit( UnitConverterUnit.SpeedKnot, GetLocalizedStringName("UnitName_Knot"), GetLocalizedStringName("UnitAbbreviation_Knot"), 6 ),
                new OrderedUnit( UnitConverterUnit.SpeedMach, GetLocalizedStringName("UnitName_Mach"), GetLocalizedStringName("UnitAbbreviation_Mach"), 7 ),
                new OrderedUnit( UnitConverterUnit.SpeedMetersPerSecond, GetLocalizedStringName("UnitName_MetersPerSecond"), GetLocalizedStringName("UnitAbbreviation_MetersPerSecond"), 2 ),
                new OrderedUnit( UnitConverterUnit.SpeedMilesPerHour, GetLocalizedStringName("UnitName_MilesPerHour"), GetLocalizedStringName("UnitAbbreviation_MilesPerHour"), 5, useSI, useUSCustomary, false ),
                new OrderedUnit( UnitConverterUnit.SpeedTurtle, GetLocalizedStringName("UnitName_Turtle"), GetLocalizedStringName("UnitAbbreviation_Turtle"), 8, false, false, true ),
                new OrderedUnit( UnitConverterUnit.SpeedHorse, GetLocalizedStringName("UnitName_Horse"), GetLocalizedStringName("UnitAbbreviation_Horse"), 9, false, false, true ),
                new OrderedUnit( UnitConverterUnit.SpeedJet, GetLocalizedStringName("UnitName_Jet"), GetLocalizedStringName("UnitAbbreviation_Jet"), 10, false, false, true )
            };
            unitMap.Add(ViewMode.Speed, speedUnits);

            // --- Volume Units ---
            var volumeUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnit.VolumeCubicCentimeter, GetLocalizedStringName("UnitName_CubicCentimeter"), GetLocalizedStringName("UnitAbbreviation_CubicCentimeter"), 2 ),
                new OrderedUnit( UnitConverterUnit.VolumeCubicFoot, GetLocalizedStringName("UnitName_CubicFoot"), GetLocalizedStringName("UnitAbbreviation_CubicFoot"), 13 ),
                new OrderedUnit( UnitConverterUnit.VolumeCubicInch, GetLocalizedStringName("UnitName_CubicInch"), GetLocalizedStringName("UnitAbbreviation_CubicInch"), 12 ),
                new OrderedUnit( UnitConverterUnit.VolumeCubicMeter, GetLocalizedStringName("UnitName_CubicMeter"), GetLocalizedStringName("UnitAbbreviation_CubicMeter"), 4 ),
                new OrderedUnit( UnitConverterUnit.VolumeCubicYard, GetLocalizedStringName("UnitName_CubicYard"), GetLocalizedStringName("UnitAbbreviation_CubicYard"), 14 ),
                new OrderedUnit( UnitConverterUnit.VolumeCupUS, GetLocalizedStringName("UnitName_CupUS"), GetLocalizedStringName("UnitAbbreviation_CupUS"), 8 ),
                new OrderedUnit( UnitConverterUnit.VolumeFluidOunceUK, GetLocalizedStringName("UnitName_FluidOunceUK"), GetLocalizedStringName("UnitAbbreviation_FluidOunceUK"), 17 ),
                new OrderedUnit( UnitConverterUnit.VolumeFluidOunceUS, GetLocalizedStringName("UnitName_FluidOunceUS"), GetLocalizedStringName("UnitAbbreviation_FluidOunceUS"), 7 ),
                new OrderedUnit( UnitConverterUnit.VolumeGallonUK, GetLocalizedStringName("UnitName_GallonUK"), GetLocalizedStringName("UnitAbbreviation_GallonUK"), 20 ),
                new OrderedUnit( UnitConverterUnit.VolumeGallonUS, GetLocalizedStringName("UnitName_GallonUS"), GetLocalizedStringName("UnitAbbreviation_GallonUS"), 11 ),
                new OrderedUnit( UnitConverterUnit.VolumeLiter, GetLocalizedStringName("UnitName_Liter"), GetLocalizedStringName("UnitAbbreviation_Liter"), 3 ),
                new OrderedUnit( UnitConverterUnit.VolumeMilliliter, GetLocalizedStringName("UnitName_Milliliter"), GetLocalizedStringName("UnitAbbreviation_Milliliter"), 1, useUSCustomary, useSI, false ),
                new OrderedUnit( UnitConverterUnit.VolumePintUK, GetLocalizedStringName("UnitName_PintUK"), GetLocalizedStringName("UnitAbbreviation_PintUK"), 18 ),
                new OrderedUnit( UnitConverterUnit.VolumePintUS, GetLocalizedStringName("UnitName_PintUS"), GetLocalizedStringName("UnitAbbreviation_PintUS"), 9 ),
                new OrderedUnit( UnitConverterUnit.VolumeTablespoonUS, GetLocalizedStringName("UnitName_TablespoonUS"), GetLocalizedStringName("UnitAbbreviation_TablespoonUS"), 6 ),
                new OrderedUnit( UnitConverterUnit.VolumeTeaspoonUS, GetLocalizedStringName("UnitName_TeaspoonUS"), GetLocalizedStringName("UnitAbbreviation_TeaspoonUS"), 5, useSI, useUSCustomary && m_currentRegionCode != "GB", false ),
                new OrderedUnit( UnitConverterUnit.VolumeQuartUK, GetLocalizedStringName("UnitName_QuartUK"), GetLocalizedStringName("UnitAbbreviation_QuartUK"), 19 ),
                new OrderedUnit( UnitConverterUnit.VolumeQuartUS, GetLocalizedStringName("UnitName_QuartUS"), GetLocalizedStringName("UnitAbbreviation_QuartUS"), 10 ),
                new OrderedUnit( UnitConverterUnit.VolumeTeaspoonUK, GetLocalizedStringName("UnitName_TeaspoonUK"), GetLocalizedStringName("UnitAbbreviation_TeaspoonUK"), 15, false, useUSCustomary && m_currentRegionCode == "GB", false ),
                new OrderedUnit( UnitConverterUnit.VolumeTablespoonUK, GetLocalizedStringName("UnitName_TablespoonUK"), GetLocalizedStringName("UnitAbbreviation_TablespoonUK"), 16 ),
                new OrderedUnit( UnitConverterUnit.VolumeCoffeeCup, GetLocalizedStringName("UnitName_CoffeeCup"), GetLocalizedStringName("UnitAbbreviation_CoffeeCup"), 22, false, false, true ),
                new OrderedUnit( UnitConverterUnit.VolumeBathtub, GetLocalizedStringName("UnitName_Bathtub"), GetLocalizedStringName("UnitAbbreviation_Bathtub"), 23, false, false, true ),
                new OrderedUnit( UnitConverterUnit.VolumeSwimmingPool, GetLocalizedStringName("UnitName_SwimmingPool"), GetLocalizedStringName("UnitAbbreviation_SwimmingPool"), 24, false, false, true )
            };
            unitMap.Add(ViewMode.Volume, volumeUnits);

            // --- Weight Units ---
            var weightUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnit.WeightCarat, GetLocalizedStringName("UnitName_Carat"), GetLocalizedStringName("UnitAbbreviation_Carat"), 1 ),
                new OrderedUnit( UnitConverterUnit.WeightCentigram, GetLocalizedStringName("UnitName_Centigram"), GetLocalizedStringName("UnitAbbreviation_Centigram"), 3 ),
                new OrderedUnit( UnitConverterUnit.WeightDecigram, GetLocalizedStringName("UnitName_Decigram"), GetLocalizedStringName("UnitAbbreviation_Decigram"), 4 ),
                new OrderedUnit( UnitConverterUnit.WeightDecagram, GetLocalizedStringName("UnitName_Decagram"), GetLocalizedStringName("UnitAbbreviation_Decagram"), 6 ),
                new OrderedUnit( UnitConverterUnit.WeightGram, GetLocalizedStringName("UnitName_Gram"), GetLocalizedStringName("UnitAbbreviation_Gram"), 5 ),
                new OrderedUnit( UnitConverterUnit.WeightHectogram, GetLocalizedStringName("UnitName_Hectogram"), GetLocalizedStringName("UnitAbbreviation_Hectogram"), 7 ),
                new OrderedUnit( UnitConverterUnit.WeightKilogram, GetLocalizedStringName("UnitName_Kilogram"), GetLocalizedStringName("UnitAbbreviation_Kilogram"), 8, useUSCustomary, useSI, false ),
                new OrderedUnit( UnitConverterUnit.WeightLongTon, GetLocalizedStringName("UnitName_LongTon"), GetLocalizedStringName("UnitAbbreviation_LongTon"), 14 ),
                new OrderedUnit( UnitConverterUnit.WeightMilligram, GetLocalizedStringName("UnitName_Milligram"), GetLocalizedStringName("UnitAbbreviation_Milligram"), 2 ),
                new OrderedUnit( UnitConverterUnit.WeightOunce, GetLocalizedStringName("UnitName_Ounce"), GetLocalizedStringName("UnitAbbreviation_Ounce"), 10 ),
                new OrderedUnit( UnitConverterUnit.WeightPound, GetLocalizedStringName("UnitName_Pound"), GetLocalizedStringName("UnitAbbreviation_Pound"), 11, useSI, useUSCustomary, false ),
                new OrderedUnit( UnitConverterUnit.WeightShortTon, GetLocalizedStringName("UnitName_ShortTon"), GetLocalizedStringName("UnitAbbreviation_ShortTon"), 13 ),
                new OrderedUnit( UnitConverterUnit.WeightStone, GetLocalizedStringName("UnitName_Stone"), GetLocalizedStringName("UnitAbbreviation_Stone"), 12 ),
                new OrderedUnit( UnitConverterUnit.WeightTonne, GetLocalizedStringName("UnitName_Tonne"), GetLocalizedStringName("UnitAbbreviation_Tonne"), 9 ),
                new OrderedUnit( UnitConverterUnit.WeightSnowflake, GetLocalizedStringName("UnitName_Snowflake"), GetLocalizedStringName("UnitAbbreviation_Snowflake"), 15, false, false, true ),
                new OrderedUnit( UnitConverterUnit.WeightSoccerBall, GetLocalizedStringName("UnitName_SoccerBall"), GetLocalizedStringName("UnitAbbreviation_SoccerBall"), 16, false, false, true ),
                new OrderedUnit( UnitConverterUnit.WeightElephant, GetLocalizedStringName("UnitName_Elephant"), GetLocalizedStringName("UnitAbbreviation_Elephant"), 17, false, false, true ),
                new OrderedUnit( UnitConverterUnit.WeightWhale, GetLocalizedStringName("UnitName_Whale"), GetLocalizedStringName("UnitAbbreviation_Whale"), 18, false, false, true )
            };
            unitMap.Add(ViewMode.Weight, weightUnits);

            // --- Pressure Units ---
            var pressureUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnit.PressureAtmosphere, GetLocalizedStringName("UnitName_Atmosphere"), GetLocalizedStringName("UnitAbbreviation_Atmosphere"), 1, true, false, false ),
                new OrderedUnit( UnitConverterUnit.PressureBar, GetLocalizedStringName("UnitName_Bar"), GetLocalizedStringName("UnitAbbreviation_Bar"), 2, false, true, false ),
                new OrderedUnit( UnitConverterUnit.PressureKiloPascal, GetLocalizedStringName("UnitName_KiloPascal"), GetLocalizedStringName("UnitAbbreviation_KiloPascal"), 3 ),
                new OrderedUnit( UnitConverterUnit.PressureMillimeterOfMercury, GetLocalizedStringName("UnitName_MillimeterOfMercury "), GetLocalizedStringName("UnitAbbreviation_MillimeterOfMercury "), 4 ), // Note trailing space in C++ resource keys
                new OrderedUnit( UnitConverterUnit.PressurePascal, GetLocalizedStringName("UnitName_Pascal"), GetLocalizedStringName("UnitAbbreviation_Pascal"), 5 ),
                new OrderedUnit( UnitConverterUnit.PressurePSI, GetLocalizedStringName("UnitName_PSI"), GetLocalizedStringName("UnitAbbreviation_PSI"), 6, false, false, false )
            };
            unitMap.Add(ViewMode.Pressure, pressureUnits);

            // --- Angle Units ---
            var angleUnits = new List<OrderedUnit> {
                new OrderedUnit( UnitConverterUnit.AngleDegree, GetLocalizedStringName("UnitName_Degree"), GetLocalizedStringName("UnitAbbreviation_Degree"), 1, true, false, false ),
                new OrderedUnit( UnitConverterUnit.AngleRadian, GetLocalizedStringName("UnitName_Radian"), GetLocalizedStringName("UnitAbbreviation_Radian"), 2, false, true, false ),
                new OrderedUnit( UnitConverterUnit.AngleGradian, GetLocalizedStringName("UnitName_Gradian"), GetLocalizedStringName("UnitAbbreviation_Gradian"), 3 )
            };
            unitMap.Add(ViewMode.Angle, angleUnits);

            return unitMap;
        }

        private static Dictionary<ViewMode, Dictionary<int, double>> GetConversionData()
        {
            /*categoryId, UnitId, factor*/
            var unitDataList = new List<UnitData> {
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnit.AreaAcre, Factor = 4046.8564224 },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnit.AreaSquareMeter, Factor = 1 },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnit.AreaSquareFoot, Factor = 0.09290304 },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnit.AreaSquareYard, Factor = 0.83612736 },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnit.AreaSquareMillimeter, Factor = 0.000001 },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnit.AreaSquareCentimeter, Factor = 0.0001 },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnit.AreaSquareInch, Factor = 0.00064516 },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnit.AreaSquareMile, Factor = 2589988.110336 },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnit.AreaSquareKilometer, Factor = 1000000 },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnit.AreaHectare, Factor = 10000 },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnit.AreaHand, Factor = 0.012516104 },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnit.AreaPaper, Factor = 0.06032246 },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnit.AreaSoccerField, Factor = 10869.66 },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnit.AreaCastle, Factor = 100000 },
                new UnitData { CategoryId = ViewMode.Area, UnitId = UnitConverterUnit.AreaPyeong, Factor = 400.0 / 121.0 },

                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataBit, Factor = 0.000000125 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataNibble, Factor = 0.0000005 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataByte, Factor = 0.000001 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataKilobyte, Factor = 0.001 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataMegabyte, Factor = 1 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataGigabyte, Factor = 1000 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataTerabyte, Factor = 1000000 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataPetabyte, Factor = 1000000000 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataExabytes, Factor = 1000000000000 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataZetabytes, Factor = 1000000000000000 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataYottabyte, Factor = 1000000000000000000 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataKilobit, Factor = 0.000125 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataMegabit, Factor = 0.125 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataGigabit, Factor = 125 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataTerabit, Factor = 125000 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataPetabit, Factor = 125000000 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataExabits, Factor = 125000000000 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataZetabits, Factor = 125000000000000 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataYottabit, Factor = 125000000000000000 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataGibibits, Factor = 134.217728 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataGibibytes, Factor = 1073.741824 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataKibibits, Factor = 0.000128 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataKibibytes, Factor = 0.001024 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataMebibits, Factor = 0.131072 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataMebibytes, Factor = 1.048576 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataPebibits, Factor = 140737488.355328 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataPebibytes, Factor = 1125899906.842624 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataTebibits, Factor = 137438.953472 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataTebibytes, Factor = 1099511.627776 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataExbibits, Factor = 144115188075.855872 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataExbibytes, Factor = 1152921504606.846976 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataZebibits, Factor = 147573952589676.412928 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataZebibytes, Factor = 1180591620717411.303424 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataYobibits, Factor = 151115727451828646.838272 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataYobibytes, Factor = 1208925819614629174.706176 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataFloppyDisk, Factor = 1.474560 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataCD, Factor = 700 },
                new UnitData { CategoryId = ViewMode.Data, UnitId = UnitConverterUnit.DataDVD, Factor = 4700 },

                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnit.EnergyCalorie, Factor = 4.184 },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnit.EnergyKilocalorie, Factor = 4184 },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnit.EnergyBritishThermalUnit, Factor = 1055.056 },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnit.EnergyKilojoule, Factor = 1000 },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnit.EnergyKilowatthour, Factor = 3600000 },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnit.EnergyElectronVolt, Factor = 0.0000000000000000001602176565 },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnit.EnergyJoule, Factor = 1 },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnit.EnergyFootPound, Factor = 1.3558179483314 },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnit.EnergyBattery, Factor = 9000 },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnit.EnergyBanana, Factor = 439614 },
                new UnitData { CategoryId = ViewMode.Energy, UnitId = UnitConverterUnit.EnergySliceOfCake, Factor = 1046700 },

                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnit.LengthInch, Factor = 0.0254 },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnit.LengthFoot, Factor = 0.3048 },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnit.LengthYard, Factor = 0.9144 },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnit.LengthMile, Factor = 1609.344 },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnit.LengthMicron, Factor = 0.000001 },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnit.LengthMillimeter, Factor = 0.001 },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnit.LengthNanometer, Factor = 0.000000001 },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnit.LengthAngstrom, Factor = 0.0000000001 },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnit.LengthCentimeter, Factor = 0.01 },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnit.LengthMeter, Factor = 1 },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnit.LengthKilometer, Factor = 1000 },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnit.LengthNauticalMile, Factor = 1852 },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnit.LengthPaperclip, Factor = 0.035052 },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnit.LengthHand, Factor = 0.18669 },
                new UnitData { CategoryId = ViewMode.Length, UnitId = UnitConverterUnit.LengthJumboJet, Factor = 76 },

                new UnitData { CategoryId = ViewMode.Power, UnitId = UnitConverterUnit.PowerBritishThermalUnitPerMinute, Factor = 17.58426666666667 },
                new UnitData { CategoryId = ViewMode.Power, UnitId = UnitConverterUnit.PowerFootPoundPerMinute, Factor = 0.0225969658055233 },
                new UnitData { CategoryId = ViewMode.Power, UnitId = UnitConverterUnit.PowerWatt, Factor = 1 },
                new UnitData { CategoryId = ViewMode.Power, UnitId = UnitConverterUnit.PowerKilowatt, Factor = 1000 },
                new UnitData { CategoryId = ViewMode.Power, UnitId = UnitConverterUnit.PowerHorsepower, Factor = 745.69987158227022 },
                new UnitData { CategoryId = ViewMode.Power, UnitId = UnitConverterUnit.PowerLightBulb, Factor = 60 },
                new UnitData { CategoryId = ViewMode.Power, UnitId = UnitConverterUnit.PowerHorse, Factor = 745.7 },
                new UnitData { CategoryId = ViewMode.Power, UnitId = UnitConverterUnit.PowerTrainEngine, Factor = 2982799.486329081 },

                new UnitData { CategoryId = ViewMode.Time, UnitId = UnitConverterUnit.TimeDay, Factor = 86400 },
                new UnitData { CategoryId = ViewMode.Time, UnitId = UnitConverterUnit.TimeSecond, Factor = 1 },
                new UnitData { CategoryId = ViewMode.Time, UnitId = UnitConverterUnit.TimeWeek, Factor = 604800 },
                new UnitData { CategoryId = ViewMode.Time, UnitId = UnitConverterUnit.TimeYear, Factor = 31557600 },
                new UnitData { CategoryId = ViewMode.Time, UnitId = UnitConverterUnit.TimeMillisecond, Factor = 0.001 },
                new UnitData { CategoryId = ViewMode.Time, UnitId = UnitConverterUnit.TimeMicrosecond, Factor = 0.000001 },
                new UnitData { CategoryId = ViewMode.Time, UnitId = UnitConverterUnit.TimeMinute, Factor = 60 },
                new UnitData { CategoryId = ViewMode.Time, UnitId = UnitConverterUnit.TimeHour, Factor = 3600 },

                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeCupUS, Factor = 236.588237 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumePintUS, Factor = 473.176473 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumePintUK, Factor = 568.26125 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeQuartUS, Factor = 946.352946 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeQuartUK, Factor = 1136.5225 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeGallonUS, Factor = 3785.411784 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeGallonUK, Factor = 4546.09 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeLiter, Factor = 1000 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeTeaspoonUS, Factor = 4.92892159375 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeTablespoonUS, Factor = 14.78676478125 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeCubicCentimeter, Factor = 1 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeCubicYard, Factor = 764554.857984 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeCubicMeter, Factor = 1000000 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeMilliliter, Factor = 1 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeCubicInch, Factor = 16.387064 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeCubicFoot, Factor = 28316.846592 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeFluidOunceUS, Factor = 29.5735295625 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeFluidOunceUK, Factor = 28.4130625 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeTeaspoonUK, Factor = 5.91938802083333333333 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeTablespoonUK, Factor = 17.7581640625 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeCoffeeCup, Factor = 236.5882 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeBathtub, Factor = 378541.2 },
                new UnitData { CategoryId = ViewMode.Volume, UnitId = UnitConverterUnit.VolumeSwimmingPool, Factor = 3750000000 },

                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightKilogram, Factor = 1 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightHectogram, Factor = 0.1 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightDecagram, Factor = 0.01 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightGram, Factor = 0.001 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightPound, Factor = 0.45359237 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightOunce, Factor = 0.028349523125 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightMilligram, Factor = 0.000001 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightCentigram, Factor = 0.00001 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightDecigram, Factor = 0.0001 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightLongTon, Factor = 1016.0469088 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightTonne, Factor = 1000 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightStone, Factor = 6.35029318 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightCarat, Factor = 0.0002 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightShortTon, Factor = 907.18474 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightSnowflake, Factor = 0.000002 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightSoccerBall, Factor = 0.4325 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightElephant, Factor = 4000 },
                new UnitData { CategoryId = ViewMode.Weight, UnitId = UnitConverterUnit.WeightWhale, Factor = 90000 },

                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnit.SpeedCentimetersPerSecond, Factor = 1 },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnit.SpeedFeetPerSecond, Factor = 30.48 },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnit.SpeedKilometersPerHour, Factor = 27.777777777777777777778 },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnit.SpeedKnot, Factor = 51.44 },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnit.SpeedMach, Factor = 34030 },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnit.SpeedMetersPerSecond, Factor = 100 },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnit.SpeedMilesPerHour, Factor = 44.7 },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnit.SpeedTurtle, Factor = 8.94 },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnit.SpeedHorse, Factor = 2011.5 },
                new UnitData { CategoryId = ViewMode.Speed, UnitId = UnitConverterUnit.SpeedJet, Factor = 24585 },

                new UnitData { CategoryId = ViewMode.Angle, UnitId = UnitConverterUnit.AngleDegree, Factor = 1 },
                new UnitData { CategoryId = ViewMode.Angle, UnitId = UnitConverterUnit.AngleRadian, Factor = 57.29577951308233 },
                new UnitData { CategoryId = ViewMode.Angle, UnitId = UnitConverterUnit.AngleGradian, Factor = 0.9 },

                new UnitData { CategoryId = ViewMode.Pressure, UnitId = UnitConverterUnit.PressureAtmosphere, Factor = 1 },
                new UnitData { CategoryId = ViewMode.Pressure, UnitId = UnitConverterUnit.PressureBar, Factor = 0.9869232667160128 },
                new UnitData { CategoryId = ViewMode.Pressure, UnitId = UnitConverterUnit.PressureKiloPascal, Factor = 0.0098692326671601 },
                new UnitData { CategoryId = ViewMode.Pressure, UnitId = UnitConverterUnit.PressureMillimeterOfMercury, Factor = 0.0013155687145324 },
                new UnitData { CategoryId = ViewMode.Pressure, UnitId = UnitConverterUnit.PressurePascal, Factor = 9.869232667160128e-6 },
                new UnitData { CategoryId = ViewMode.Pressure, UnitId = UnitConverterUnit.PressurePSI, Factor = 0.068045961016531 }
            };

            var categoryToUnitConversionMap = new Dictionary<ViewMode, Dictionary<int, double>>();

            // Populate the hash map and return;
            foreach (UnitData unitdata in unitDataList)
            {
                if (!categoryToUnitConversionMap.TryGetValue(unitdata.CategoryId, out Dictionary<int, double> conversionData))
                {
                    conversionData = new Dictionary<int, double>();
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
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnit.TemperatureDegreesCelsius, UnitConverterUnit.TemperatureDegreesCelsius, 1, 0 ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnit.TemperatureDegreesCelsius, UnitConverterUnit.TemperatureDegreesFahrenheit, 1.8, 32 ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnit.TemperatureDegreesCelsius, UnitConverterUnit.TemperatureKelvin, 1, 273.15 ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnit.TemperatureDegreesFahrenheit, UnitConverterUnit.TemperatureDegreesCelsius, 0.55555555555555555555555555555556, -32, CONVERT_WITH_OFFSET_FIRST ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnit.TemperatureDegreesFahrenheit, UnitConverterUnit.TemperatureDegreesFahrenheit, 1, 0 ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnit.TemperatureDegreesFahrenheit, UnitConverterUnit.TemperatureKelvin, 0.55555555555555555555555555555556, 459.67, CONVERT_WITH_OFFSET_FIRST ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnit.TemperatureKelvin, UnitConverterUnit.TemperatureDegreesCelsius, 1, -273.15, CONVERT_WITH_OFFSET_FIRST ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnit.TemperatureKelvin, UnitConverterUnit.TemperatureDegreesFahrenheit, 1.8, -459.67 ),
                new ExplicitUnitConversionData( ViewMode.Temperature, UnitConverterUnit.TemperatureKelvin, UnitConverterUnit.TemperatureKelvin, 1, 0 )
            };

            var unitToUnitConversionList = new Dictionary<int, Dictionary<int, CalcManager.ConversionData>>();

            // Populate the hash map and return;
            foreach (ExplicitUnitConversionData data in conversionDataList)
            {
                if (!unitToUnitConversionList.TryGetValue((int)data.ParentUnitId, out Dictionary<int, CalcManager.ConversionData> conversionData))
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
