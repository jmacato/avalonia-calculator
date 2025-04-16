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
using Microsoft.Windows.System;
using ViewModeType = CalculatorApp.ViewModel.Common.ViewMode;
using UCM = UnitConversionManager;
using System.Diagnostics;
using Windows.System;


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
        new NavCategoryInitializer(ViewMode.Standard,
            STANDARD_ID,
            "Standard",
            "StandardMode",
            "\uE8EF",
            CategoryGroupType.Calculator,
            MyVirtualKey.Number1,
            "1",
            SUPPORTS_ALL),
        new NavCategoryInitializer(ViewMode.Scientific,
            SCIENTIFIC_ID,
            "Scientific",
            "ScientificMode",
            "\uF196",
            CategoryGroupType.Calculator,
            MyVirtualKey.Number2,
            "2",
            SUPPORTS_ALL),
        new NavCategoryInitializer(ViewMode.Graphing,
            GRAPHING_ID,
            "Graphing",
            "GraphingCalculatorMode",
            "\uF770",
            CategoryGroupType.Calculator,
            MyVirtualKey.Number3,
            "3",
            SUPPORTS_ALL),
        new NavCategoryInitializer(ViewMode.Programmer,
            PROGRAMMER_ID,
            "Programmer",
            "ProgrammerMode",
            "\uECCE",
            CategoryGroupType.Calculator,
            MyVirtualKey.Number4,
            "4",
            SUPPORTS_ALL),
        new NavCategoryInitializer(ViewMode.Date,
            DATE_ID,
            "Date",
            "DateCalculationMode",
            "\uE787",
            CategoryGroupType.Calculator,
            MyVirtualKey.Number5,
            "5",
            SUPPORTS_ALL),
        new NavCategoryInitializer(ViewMode.Currency,
            CURRENCY_ID,
            "Currency",
            "CategoryName_Currency",
            "\uEB0D",
            CategoryGroupType.Converter,
            MyVirtualKey.None,
            null,
            POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Volume,
            VOLUME_ID,
            "Volume",
            "CategoryName_Volume",
            "\uF1AA",
            CategoryGroupType.Converter,
            MyVirtualKey.None,
            null,
            POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Length,
            LENGTH_ID,
            "Length",
            "CategoryName_Length",
            "\uECC6",
            CategoryGroupType.Converter,
            MyVirtualKey.None,
            null,
            POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Weight,
            WEIGHT_ID,
            "Weight and Mass",
            "CategoryName_Weight",
            "\uF4C1",
            CategoryGroupType.Converter,
            MyVirtualKey.None,
            null,
            POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Temperature,
            TEMPERATURE_ID,
            "Temperature",
            "CategoryName_Temperature",
            "\uE7A3",
            CategoryGroupType.Converter,
            MyVirtualKey.None,
            null,
            SUPPORTS_NEGATIVE),
        new NavCategoryInitializer(ViewMode.Energy,
            ENERGY_ID,
            "Energy",
            "CategoryName_Energy",
            "\uECAD",
            CategoryGroupType.Converter,
            MyVirtualKey.None,
            null,
            POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Area,
            AREA_ID,
            "Area",
            "CategoryName_Area",
            "\uE809",
            CategoryGroupType.Converter,
            MyVirtualKey.None,
            null,
            POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Speed,
            SPEED_ID,
            "Speed",
            "CategoryName_Speed",
            "\uEADA",
            CategoryGroupType.Converter,
            MyVirtualKey.None,
            null,
            POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Time,
            TIME_ID,
            "Time",
            "CategoryName_Time",
            "\uE917",
            CategoryGroupType.Converter,
            MyVirtualKey.None,
            null,
            POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Power,
            POWER_ID,
            "Power",
            "CategoryName_Power",
            "\uE945",
            CategoryGroupType.Converter,
            MyVirtualKey.None,
            null,
            SUPPORTS_NEGATIVE),
        new NavCategoryInitializer(ViewMode.Data,
            DATA_ID,
            "Data",
            "CategoryName_Data",
            "\uF20F",
            CategoryGroupType.Converter,
            MyVirtualKey.None,
            null,
            POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Pressure,
            PRESSURE_ID,
            "Pressure",
            "CategoryName_Pressure",
            "\uEC4A",
            CategoryGroupType.Converter,
            MyVirtualKey.None,
            null,
            POSITIVE_ONLY),
        new NavCategoryInitializer(ViewMode.Angle,
            ANGLE_ID,
            "Angle",
            "CategoryName_Angle",
            "\uF515",
            CategoryGroupType.Converter,
            MyVirtualKey.None,
            null,
            SUPPORTS_NEGATIVE),
    };


    public static bool IsCalculatorViewMode(ViewModeType mode)
    {
        // Historically, Calculator modes are Standard, Scientific, and Programmer.
        return !IsDateCalculatorViewMode(mode) && !IsGraphingCalculatorViewMode(mode) &&
               IsModeInCategoryGroup(mode, CategoryGroupType.Calculator);
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
        return s_categoryManifest.Any(initializer =>
            initializer.viewMode == mode && initializer.groupType == type);
        // });
    }
}

public partial class NavCategoryGroup
{
    public NavCategoryGroup(NavCategoryGroupInitializer groupInitializer)
    {
        m_Categories = new();
        m_GroupType = groupInitializer.type;

        var resProvider = AppResourceProvider.GetInstance();
        m_Name = resProvider.GetResourceString((groupInitializer.headerResourceKey));
        String groupMode = resProvider.GetResourceString((groupInitializer.modeResourceKey));
        String automationName = resProvider.GetResourceString((groupInitializer.automationResourceKey));

        String navCategoryHeaderAutomationNameFormat =
            resProvider.GetResourceString("NavCategoryHeader_AutomationNameFormat");
        m_AutomationName =
            LocalizationStringUtil.GetLocalizedString(navCategoryHeaderAutomationNameFormat, automationName);

        String navCategoryItemAutomationNameFormat =
            resProvider.GetResourceString("NavCategoryItem_AutomationNameFormat");

        foreach (var categoryInitializer in NavCategory.s_categoryManifest)
        {
            if (categoryInitializer.groupType == groupInitializer.type)
            {
                String nameResourceKey = (categoryInitializer.nameResourceKey);
                String categoryName = resProvider.GetResourceString(nameResourceKey + "Text");
                String categoryAutomationName =
                    LocalizationStringUtil.GetLocalizedString(navCategoryItemAutomationNameFormat, categoryName,
                        m_Name);

                m_Categories.Add(new NavCategory(
                    categoryName,
                    categoryAutomationName,
                    (categoryInitializer.glyph),
                    categoryInitializer.accessKey ?? resProvider.GetResourceString(nameResourceKey + "AccessKey"),
                    groupMode,
                    categoryInitializer.viewMode,
                    categoryInitializer.supportsNegative,
                    categoryInitializer.viewMode != ViewMode.Graphing));
            }
        }
    }


}

public partial class NavCategoryStates
{
    public static void SetCurrentUser(string userId)
    {
        CurrentUserId = userId;
    }

    public static string CurrentUserId;
    public static ObservableCollection<NavCategoryGroup> CreateMenuOptions()
    {
        var menuOptions = new ObservableCollection<NavCategoryGroup>();
        menuOptions.Add(NavCategoryStates.CreateCalculatorCategoryGroup());
        menuOptions.Add(NavCategoryStates.CreateConverterCategoryGroup());
        return menuOptions;
    }
    public static NavCategoryGroup CreateCalculatorCategoryGroup()
    {
        return new NavCategoryGroup(new NavCategoryGroupInitializer(CategoryGroupType.Calculator,
            "CalculatorModeTextCaps", "CalculatorModeText", "CalculatorModePluralText"));
    }

    public static NavCategoryGroup CreateConverterCategoryGroup()
    {
        return new NavCategoryGroup(
            new NavCategoryGroupInitializer(CategoryGroupType.Converter, "ConverterModeTextCaps", "ConverterModeText",
                "ConverterModePluralText"));
    }

// This function should only be used when storing the mode to app data.
    public static int Serialize(ViewMode mode)
    {
        // var citer = find_if(
        //     cbegin(),
        //     cend(s_categoryManifest),
        //     [mode](const auto& initializer) { return initializer.viewMode == mode; });
        //return (citer != s_categoryManifest.cend()) ? citer.serializationId : -1;

        foreach (var navCat in NavCategory.s_categoryManifest)
        {
            if (navCat.viewMode == mode)
                return navCat.serializationId;
        }

        return -1;
    }

// This function should only be used when restoring the mode from app data.
    public static ViewMode Deserialize(object obj)
    {
        // If we cast directly to ViewMode we will fail
        // because we technically store an int.
        // Need to cast to int, then ViewMode.
        // var boxed = obj as int;
        if (obj is int serializationId)
            // {
            foreach (var navCat in NavCategory.s_categoryManifest)
            {
                if (navCat.serializationId == serializationId)
                    return navCat.viewMode;
            }

        return ViewMode.None;


        //     int  = boxed.Value;
        //     var citer = find_if(
        //         cbegin(s_categoryManifest),
        //         cend(s_categoryManifest),
        //         [serializationId](const auto &initializer) {
        //         return initializer.serializationId == serializationId;
        //     });
        //
        //     return citer != s_categoryManifest.cend()
        //         ? (citer.viewMode == ViewMode.Graphing
        //             ? (IsGraphingModeEnabled() ? citer.viewMode : ViewMode.None)
        //             : citer.viewMode)
        //         : ViewMode.None;
        // }
        // else
        // {
        // }
    }

    public static ViewMode GetViewModeForFriendlyName(String name)
    {
        // {
        foreach (var navCat in NavCategory.s_categoryManifest)
        {
            if (navCat.friendlyName == name)
                return navCat.viewMode;
        }

        return ViewMode.None;
        //
        // const auto &citer = find_if(
        //     cbegin(s_categoryManifest),
        //     cend(s_categoryManifest),
        //     [name](const auto &initializer) {
        //     return wcscmp(initializer.friendlyName, name.Data()) == 0;
        // });
        //
        // return (citer != s_categoryManifest.cend()) ? citer.viewMode : ViewMode.None;
    }

    public static String GetFriendlyName(ViewMode mode)
    {
        foreach (var navCat in NavCategory.s_categoryManifest)
        {
            if (navCat.viewMode == mode)
                return navCat.friendlyName;
        }

        return "None";

        // const auto &citer = find_if(
        //     cbegin(s_categoryManifest),
        //     cend(s_categoryManifest),
        //     [mode](const auto &initializer) {
        //     return initializer.viewMode == mode;
        // });
        //
        // return (citer != s_categoryManifest.cend()) ? (citer.friendlyName) : "None";
    }

    public static String GetNameResourceKey(ViewMode mode)
    {
        foreach (var navCat in NavCategory.s_categoryManifest)
        {
            if (navCat.viewMode == mode)
                return navCat.nameResourceKey;
        }

        return "Text";

        // const auto &citer = find_if(
        //     cbegin(s_categoryManifest),
        //     cend(s_categoryManifest),
        //     [mode](const auto &initializer) {
        //     return initializer.viewMode == mode;
        // });
        //
        // return (citer != s_categoryManifest.cend()) ? (citer.nameResourceKey) + "Text" : null;
    }

    public static CategoryGroupType GetGroupType(ViewMode mode)
    {
        Debugger.Break();
        foreach (var navCat in NavCategory.s_categoryManifest)
        {
            if (navCat.viewMode == mode)
                return navCat.groupType;
        }

        return CategoryGroupType.None;
        //
        // const auto &citer = find_if(
        //     cbegin(s_categoryManifest),
        //     cend(s_categoryManifest),
        //     [mode](const auto &initializer) {
        //     return initializer.viewMode == mode;
        // });
        //
        // return (citer != s_categoryManifest.cend()) ? citer.groupType : CategoryGroupType.None;
    }

    // GetIndex is 0-based, GetPosition is 1-based
    public static int GetIndex(ViewMode mode)
    {
        int position = GetPosition(mode);
        return Math.Max(-1, position - 1);
    }

    public static int GetFlatIndex(ViewMode mode)
    {
        // int index = -1;


        int index = -1;
        CategoryGroupType type = CategoryGroupType.None;

        foreach (var initializer in NavCategory.s_categoryManifest)
        {
            ++index;
            if (initializer.groupType != type)
            {
                type = initializer.groupType;
                ++index;
            }

            if (initializer.viewMode == mode)
            {
                return index;
            }
        }

        return -1;


        // CategoryGroupType type = CategoryGroupType.None;
        // const auto &citer = find_if(
        //     cbegin(s_categoryManifest),
        //     cend(s_categoryManifest),
        //     [mode, &type, &index](const auto &initializer) {
        //     ++index;
        //     if (initializer.groupType != type)
        //     {
        //         type = initializer.groupType;
        //         ++index;
        //     }
        //
        //     return initializer.viewMode == mode;
        // });
        //
        // return (citer != s_categoryManifest.cend()) ? index : -1;
    }

    // GetIndex is 0-based, GetPosition is 1-based
    public static int GetIndexInGroup(ViewMode mode, CategoryGroupType type)
    {
        int index = -1;
        foreach (var initializer in NavCategory.s_categoryManifest.Where(x=>x.groupType == type))
        {
            ++index;

            if (initializer.viewMode == mode)
            {
                break;
            }
        }

        // var k = NavCategory.s_categoryManifest.Select((x,i)=>(x,i)).Where(x=>x.x.groupType == type && x.x.viewMode == mode).Select(x=> x.i).FirstOrDefault();

        return index;
    }

    // GetIndex is 0-based, GetPosition is 1-based
    public static int GetPosition(ViewMode mode)
    {
        int position = 0;
        foreach (var initializer in NavCategory.s_categoryManifest)
        {
            if (initializer.viewMode == mode)
            {
                ++position;
                break;
            }
        }

        return position;

        // int position = 0;
        // const auto &citer = find_if(
        //     cbegin(s_categoryManifest),
        //     cend(s_categoryManifest),
        //     [mode, &position](const auto &initializer) {
        //     ++position;
        //     return initializer.viewMode == mode;
        // });
        //
        // return (citer != s_categoryManifest.cend()) ? position : -1;
    }

    public static ViewMode GetViewModeForVirtualKey(MyVirtualKey virtualKey)
    {
        foreach (var navCat in NavCategory.s_categoryManifest)
        {
            if (navCat.virtualKey == virtualKey)
                return navCat.viewMode;
        }

        return ViewMode.None;

        // const auto &citer = find_if(
        //     cbegin(s_categoryManifest),
        //     cend(s_categoryManifest),
        //     [virtualKey](const auto &initializer) {
        //     return initializer.virtualKey == virtualKey;
        // });
        //
        // return (citer != s_categoryManifest.end()) ? citer.viewMode : ViewMode.None;
    }

    public static void GetCategoryAcceleratorKeys(IList<MyVirtualKey> accelerators)
    {
        if (accelerators != null)
        {
            accelerators.Clear();
            foreach (var category in NavCategory.s_categoryManifest)
            {
                if (category.virtualKey != MyVirtualKey.None)
                {
                    accelerators.Add(category.virtualKey);
                }
            }
        }
    }

    public static bool IsValidViewMode(ViewMode mode)
    {
        return NavCategory.s_categoryManifest.Any(initializer => initializer.viewMode == mode);

        // const auto &citer = find_if(
        //     cbegin(s_categoryManifest),
        //     cend(s_categoryManifest),
        //     [mode](const auto &initializer) {
        //     return initializer.viewMode == mode;
        // });
        //
        // return citer != s_categoryManifest.cend();
    }

    public static bool IsViewModeEnabled(ViewMode mode)
    {
        return mode != ViewMode.Graphing || NavCategoryStates.IsGraphingModeEnabled();
    } 



    public static bool IsGraphingModeEnabled()
    {
        var user = User.GetFromId(CurrentUserId);
        if (user == null)
        {
            return true;
        }

        return NamedPolicy.GetPolicyFromPathForUser(user, "Education", "AllowGraphingCalculator").GetBoolean();
    }
}
