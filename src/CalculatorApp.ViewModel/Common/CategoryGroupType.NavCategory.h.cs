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
        public enum CategoryGroupType
        {
            None = -1,
            Calculator = 0,
            Converter = 1
        };
    }
}
