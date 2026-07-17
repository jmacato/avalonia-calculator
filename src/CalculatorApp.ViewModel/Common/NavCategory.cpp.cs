// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// #include  "pch.h"
// #include  "NavCategory.h"
// #include  "AppResourceProvider.h"
// #include  "Common/LocalizationStringUtil.h"
// #include  <initializer_list>
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CalculatorApp;
using CalculatorApp.ViewModel.Common;
using CalculatorApp.ViewModel;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Management.Policies;
using Windows.System;
using ViewModeType = CalculatorApp.ViewModel.Common.ViewMode;
using UCM = UnitConversionManager;
using System.Diagnostics;

namespace CalculatorApp.ViewModel.Common;
//^^ THESE CONSTANTS SHOULD NEVER CHANGE^^
public partial class NavCategory : INotifyPropertyChanged
{
    // Calculator categories always support negative and positive.
    const bool SUPPORTS_ALL = true;
    // Converter categories usually only support positive.
    const bool SUPPORTS_NEGATIVE = true;
    const bool POSITIVE_ONLY = false;
    // vvv THESE CONSTANTS SHOULD NEVER CHANGE vvv
    const int STANDARD_ID = 0;
    const int SCIENTIFIC_ID = 1;
    const int PROGRAMMER_ID = 2;
    const int DATE_ID = 3;
    const int VOLUME_ID = 4;
    const int LENGTH_ID = 5;
    const int WEIGHT_ID = 6;
    const int TEMPERATURE_ID = 7;
    const int ENERGY_ID = 8;
    const int AREA_ID = 9;
    const int SPEED_ID = 10;
    const int TIME_ID = 11;
    const int POWER_ID = 12;
    const int DATA_ID = 13;
    const int PRESSURE_ID = 14;
    const int ANGLE_ID = 15;
    const int CURRENCY_ID = 16;
    const int GRAPHING_ID = 17;
    // The order of items in this list determines the order of items in the menu.
    internal static List<NavCategoryInitializer> s_categoryManifest = new()
    {
        new NavCategoryInitializer(ViewMode.Standard, STANDARD_ID, "Standard", "StandardMode", "\uE8EF", CategoryGroupType.Calculator, MyVirtualKey.Number1, "1", SUPPORTS_ALL),
        new NavCategoryInitializer(ViewMode.Scientific, SCIENTIFIC_ID, "Scientific", "ScientificMode", "\uF196", CategoryGroupType.Calculator, MyVirtualKey.Number2, "2", SUPPORTS_ALL),
        new NavCategoryInitializer(ViewMode.Graphing, GRAPHING_ID, "Graphing", "GraphingCalculatorMode", "\uF770", CategoryGroupType.Calculator, MyVirtualKey.Number3, "3", SUPPORTS_ALL),
        new NavCategoryInitializer(ViewMode.Programmer, PROGRAMMER_ID, "Programmer", "ProgrammerMode", "\uECCE", CategoryGroupType.Calculator, MyVirtualKey.Number4, "4", SUPPORTS_ALL),
        new NavCategoryInitializer(ViewMode.Date, DATE_ID, "Date", "DateCalculationMode", "\uE787", CategoryGroupType.Calculator, MyVirtualKey.Number5, "5", SUPPORTS_ALL),
        new NavCategoryInitializer(ViewMode.Currency, CURRENCY_ID, "Currency", "CategoryName_Currency", "\uEB0D", CategoryGroupType.Converter, MyVirtualKey.None, null, POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Volume, VOLUME_ID, "Volume", "CategoryName_Volume", "\uF1AA", CategoryGroupType.Converter, MyVirtualKey.None, null, POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Length, LENGTH_ID, "Length", "CategoryName_Length", "\uECC6", CategoryGroupType.Converter, MyVirtualKey.None, null, POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Weight, WEIGHT_ID, "Weight and Mass", "CategoryName_Weight", "\uF4C1", CategoryGroupType.Converter, MyVirtualKey.None, null, POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Temperature, TEMPERATURE_ID, "Temperature", "CategoryName_Temperature", "\uE7A3", CategoryGroupType.Converter, MyVirtualKey.None, null, SUPPORTS_NEGATIVE),
        new NavCategoryInitializer(ViewMode.Energy, ENERGY_ID, "Energy", "CategoryName_Energy", "\uECAD", CategoryGroupType.Converter, MyVirtualKey.None, null, POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Area, AREA_ID, "Area", "CategoryName_Area", "\uE809", CategoryGroupType.Converter, MyVirtualKey.None, null, POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Speed, SPEED_ID, "Speed", "CategoryName_Speed", "\uEADA", CategoryGroupType.Converter, MyVirtualKey.None, null, POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Time, TIME_ID, "Time", "CategoryName_Time", "\uE917", CategoryGroupType.Converter, MyVirtualKey.None, null, POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Power, POWER_ID, "Power", "CategoryName_Power", "\uE945", CategoryGroupType.Converter, MyVirtualKey.None, null, SUPPORTS_NEGATIVE),
        new NavCategoryInitializer(ViewMode.Data, DATA_ID, "Data", "CategoryName_Data", "\uF20F", CategoryGroupType.Converter, MyVirtualKey.None, null, POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Pressure, PRESSURE_ID, "Pressure", "CategoryName_Pressure", "\uEC4A", CategoryGroupType.Converter, MyVirtualKey.None, null, POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Angle, ANGLE_ID, "Angle", "CategoryName_Angle", "\uF515", CategoryGroupType.Converter, MyVirtualKey.None, null, SUPPORTS_NEGATIVE),
    };
    public static bool IsCalculatorViewMode(ViewModeType mode)
    {
        // Historically, Calculator modes are Standard, Scientific, and Programmer.
        return !IsDateCalculatorViewMode(mode) && !IsGraphingCalculatorViewMode(mode) && IsModeInCategoryGroup(mode, CategoryGroupType.Calculator);
    }

    public static bool IsGraphingCalculatorViewMode(ViewModeType mode)
    {
        return mode == ViewModeType.Graphing;
    }

    public static bool IsDateCalculatorViewMode(ViewModeType mode)
    {
        return mode == ViewModeType.Date;
    }

    public static bool IsConverterViewMode(ViewModeType mode)
    {
        return IsModeInCategoryGroup(mode, CategoryGroupType.Converter);
    }

    public static bool IsModeInCategoryGroup(ViewModeType mode, CategoryGroupType type)
    {
        // return any_of(
        //     s_categoryManifest.cbegin(),
        //     s_categoryManifest.cend(),
        //     [mode, type](const auto& initializer) {
        return s_categoryManifest.Any(initializer => initializer.ViewMode == mode && initializer.GroupType == type);
        // });
    }
}
