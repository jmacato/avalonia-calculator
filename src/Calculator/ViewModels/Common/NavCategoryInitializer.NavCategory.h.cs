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
        internal sealed class NavCategoryInitializer
        {
            public NavCategoryInitializer(ViewMode viewMode, int serializationId, string friendlyName, string nameResourceKey, string glyph, CategoryGroupType groupType, MyVirtualKey virtualKey, string? accessKey, bool supportsNegative)
            {
                ViewMode = viewMode;
                SerializationId = serializationId;
                FriendlyName = friendlyName;
                NameResourceKey = nameResourceKey;
                Glyph = glyph;
                GroupType = groupType;
                VirtualKey = virtualKey;
                AccessKey = accessKey;
                SupportsNegative = supportsNegative;
            }

            internal ViewMode ViewMode { get; }

            internal int SerializationId { get; }

            internal string FriendlyName { get; }

            internal string NameResourceKey { get; }

            internal string Glyph { get; }

            internal CategoryGroupType GroupType { get; }

            internal MyVirtualKey VirtualKey { get; }

            internal string? AccessKey { get; }

            internal bool SupportsNegative { get; }
        }
    }
}
