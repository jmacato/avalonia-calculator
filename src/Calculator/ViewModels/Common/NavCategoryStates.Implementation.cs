// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// #include  "pch.h"
// #include  "NavCategory.h"
// #include  "AppResourceProvider.h"
// #include  "Common/LocalizationStringUtil.h"
// #include  <initializer_list>

using System.Collections.ObjectModel;

namespace CalculatorApp.ViewModel.Common;

public static partial class NavCategoryStates
{
    private static string s_currentUserId = string.Empty;

    public static void SetCurrentUser(string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);
        Volatile.Write(ref s_currentUserId, userId);
    }

    public static string CurrentUserId => Volatile.Read(ref s_currentUserId);
    public static ObservableCollection<NavCategoryGroup> CreateMenuOptions()
    {
        var menuOptions = new ObservableCollection<NavCategoryGroup>
        {
            CreateCalculatorCategoryGroup(),
            CreateConverterCategoryGroup()
        };
        return menuOptions;
    }

    public static NavCategoryGroup CreateCalculatorCategoryGroup()
    {
        return new NavCategoryGroup(new NavCategoryGroupInitializer(CategoryGroupType.Calculator, "CalculatorModeTextCaps", "CalculatorModeText", "CalculatorModePluralText"));
    }

    public static NavCategoryGroup CreateConverterCategoryGroup()
    {
        return new NavCategoryGroup(new NavCategoryGroupInitializer(CategoryGroupType.Converter, "ConverterModeTextCaps", "ConverterModeText", "ConverterModePluralText"));
    }

    // This function should only be used when storing the mode to app data.
    public static int Serialize(ViewMode mode)
    {
        // var citer = find_if(
        //     cbegin(),
        //     cend(s_categoryManifest),
        //     [mode](const auto& initializer) { return initializer.ViewMode == mode; });
        //return (citer != s_categoryManifest.cend()) ? citer.SerializationId : -1;
        foreach (var navCat in NavCategory.s_categoryManifest)
        {
            if (navCat.ViewMode == mode)
                return navCat.SerializationId;
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
                if (navCat.SerializationId == serializationId)
                    return navCat.ViewMode;
            }

        return ViewMode.None;
        //     int  = boxed.Value;
        //     var citer = find_if(
        //         cbegin(s_categoryManifest),
        //         cend(s_categoryManifest),
        //         [serializationId](const auto &initializer) {
        //         return initializer.SerializationId == serializationId;
        //     });
        //
        //     return citer != s_categoryManifest.cend()
        //         ? (citer.ViewMode == ViewMode.Graphing
        //             ? (IsGraphingModeEnabled() ? citer.ViewMode : ViewMode.None)
        //             : citer.ViewMode)
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
            if (navCat.FriendlyName == name)
                return navCat.ViewMode;
        }

        return ViewMode.None;
        //
        // const auto &citer = find_if(
        //     cbegin(s_categoryManifest),
        //     cend(s_categoryManifest),
        //     [name](const auto &initializer) {
        //     return wcscmp(initializer.FriendlyName, name.Data()) == 0;
        // });
        //
        // return (citer != s_categoryManifest.cend()) ? citer.ViewMode : ViewMode.None;
    }

    public static String GetFriendlyName(ViewMode mode)
    {
        foreach (var navCat in NavCategory.s_categoryManifest)
        {
            if (navCat.ViewMode == mode)
                return navCat.FriendlyName;
        }

        return "None";
        // const auto &citer = find_if(
        //     cbegin(s_categoryManifest),
        //     cend(s_categoryManifest),
        //     [mode](const auto &initializer) {
        //     return initializer.ViewMode == mode;
        // });
        //
        // return (citer != s_categoryManifest.cend()) ? (citer.FriendlyName) : "None";
    }

    public static String GetNameResourceKey(ViewMode mode)
    {
        foreach (var navCat in NavCategory.s_categoryManifest)
        {
            if (navCat.ViewMode == mode)
                return navCat.NameResourceKey + "Text";
        }

        return string.Empty;
        // const auto &citer = find_if(
        //     cbegin(s_categoryManifest),
        //     cend(s_categoryManifest),
        //     [mode](const auto &initializer) {
        //     return initializer.ViewMode == mode;
        // });
        //
        // return (citer != s_categoryManifest.cend()) ? (citer.NameResourceKey) + "Text" : null;
    }

    public static CategoryGroupType GetGroupType(ViewMode mode)
    {
        foreach (var navCat in NavCategory.s_categoryManifest)
        {
            if (navCat.ViewMode == mode)
                return navCat.GroupType;
        }

        return CategoryGroupType.None;
        //
        // const auto &citer = find_if(
        //     cbegin(s_categoryManifest),
        //     cend(s_categoryManifest),
        //     [mode](const auto &initializer) {
        //     return initializer.ViewMode == mode;
        // });
        //
        // return (citer != s_categoryManifest.cend()) ? citer.GroupType : CategoryGroupType.None;
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
            if (initializer.GroupType != type)
            {
                type = initializer.GroupType;
                ++index;
            }

            if (initializer.ViewMode == mode)
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
        //     if (initializer.GroupType != type)
        //     {
        //         type = initializer.GroupType;
        //         ++index;
        //     }
        //
        //     return initializer.ViewMode == mode;
        // });
        //
        // return (citer != s_categoryManifest.cend()) ? index : -1;
    }

    // GetIndex is 0-based, GetPosition is 1-based
    public static int GetIndexInGroup(ViewMode mode, CategoryGroupType type)
    {
        int index = -1;
        foreach (var initializer in NavCategory.s_categoryManifest.Where(x => x.GroupType == type))
        {
            ++index;
            if (initializer.ViewMode == mode)
            {
                break;
            }
        }

        // var k = NavCategory.s_categoryManifest.Select((x,i)=>(x,i)).Where(x=>x.x.GroupType == type && x.x.ViewMode == mode).Select(x=> x.i).FirstOrDefault();
        return index;
    }

    // GetIndex is 0-based, GetPosition is 1-based
    public static int GetPosition(ViewMode mode)
    {
        int position = 0;
        foreach (var initializer in NavCategory.s_categoryManifest)
        {
            if (initializer.ViewMode == mode)
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
        //     return initializer.ViewMode == mode;
        // });
        //
        // return (citer != s_categoryManifest.cend()) ? position : -1;
    }

    public static ViewMode GetViewModeForVirtualKey(MyVirtualKey virtualKey)
    {
        foreach (var navCat in NavCategory.s_categoryManifest)
        {
            if (navCat.VirtualKey == virtualKey)
                return navCat.ViewMode;
        }

        return ViewMode.None;
        // const auto &citer = find_if(
        //     cbegin(s_categoryManifest),
        //     cend(s_categoryManifest),
        //     [virtualKey](const auto &initializer) {
        //     return initializer.VirtualKey == virtualKey;
        // });
        //
        // return (citer != s_categoryManifest.end()) ? citer.ViewMode : ViewMode.None;
    }

    public static void GetCategoryAcceleratorKeys(IList<MyVirtualKey> accelerators)
    {
        if (accelerators != null)
        {
            accelerators.Clear();
            foreach (var category in NavCategory.s_categoryManifest)
            {
                if (category.VirtualKey != MyVirtualKey.None)
                {
                    accelerators.Add(category.VirtualKey);
                }
            }
        }
    }

    public static bool IsValidViewMode(ViewMode mode)
    {
        return NavCategory.s_categoryManifest.Any(initializer => initializer.ViewMode == mode);
        // const auto &citer = find_if(
        //     cbegin(s_categoryManifest),
        //     cend(s_categoryManifest),
        //     [mode](const auto &initializer) {
        //     return initializer.ViewMode == mode;
        // });
        //
        // return citer != s_categoryManifest.cend();
    }

    public static bool IsViewModeEnabled(ViewMode mode)
    {
        return mode != ViewMode.Graphing || IsGraphingModeEnabled();
    }

    public static bool IsGraphingModeEnabled()
    {
        // The WinUI app consulted the Windows Education NamedPolicy here. That
        // platform-only policy is intentionally not part of the Avalonia port.
        return true;
    }
}
