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

namespace CalculatorApp.ViewModel
{
    namespace Common
    {
        // Don't change the order of these enums
        // and definitely don't use int arithmetic
        // to change modes.
        public enum ViewMode
        {
            None = -1,
            Standard = 0,
            Scientific = 1,
            Programmer = 2,
            Date = 3,
            Volume = 4,
            Length = 5,
            Weight = 6,
            Temperature = 7,
            Energy = 8,
            Area = 9,
            Speed = 10,
            Time = 11,
            Power = 12,
            Data = 13,
            Pressure = 14,
            Angle = 15,
            Currency = 16,
            Graphing = 17
        };
    }
}
