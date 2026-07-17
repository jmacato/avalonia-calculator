// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
/* The NavCategory group of classes and enumerations is intended to serve
 * as a single location for storing metadata about a navigation mode.
 *
 * These .h and .cpp files:
 *     - Define the ViewMode enumeration which is used for setting the mode of the app.
 *     - Define a list of metadata associated with each ViewMode.
 *     - Define the order of groups and items in the navigation menu.
 *     - Provide a static helper function for creating the navigation menu options.
 *     - Provide static helper functions for querying information about a given ViewMode.
 */
// #pragma  once
// #include  "Utils.h"
// #include  "MyVirtualKey.h"
using System.Collections.ObjectModel;
using System.ComponentModel;
using ViewModeType = CalculatorApp.ViewModel.Common.ViewMode;

namespace CalculatorApp.ViewModel
{
    namespace Common
    {
        public static partial class NavCategoryStates
        {
            // public:
            // static void SetCurrentUser(string^ user);
            // static ObservableCollection<NavCategoryGroup> CreateMenuOptions();
            // static NavCategoryGroup CreateCalculatorCategoryGroup();
            // static NavCategoryGroup CreateConverterCategoryGroup();
            //
            // static bool IsValidViewMode(ViewMode mode);
            // static bool IsViewModeEnabled(ViewMode mode);
            //
            // // For saving/restoring last mode used.
            // static int Serialize(ViewMode mode);
            // static ViewMode Deserialize(object obj);
            //
            // // Query properties from states
            // static ViewMode GetViewModeForFriendlyName(string name);
            // static string GetFriendlyName(ViewMode mode);
            // static string GetNameResourceKey(ViewMode mode);
            // static CategoryGroupType GetGroupType(ViewMode mode);
            //
            // // GetIndex is 0-based, GetPosition is 1-based
            // static int GetIndex(ViewMode mode);
            // static int GetFlatIndex(ViewMode mode);
            // static int GetIndexInGroup(ViewMode mode, CategoryGroupType type);
            // static int GetPosition(ViewMode mode);
            //
            // // Virtual key related
            // static ViewMode GetViewModeForVirtualKey(MyVirtualKey virtualKey);
            // static void GetCategoryAcceleratorKeys(Windows.Foundation.Collections.IList<MyVirtualKey> resutls);
        };
    }
}
