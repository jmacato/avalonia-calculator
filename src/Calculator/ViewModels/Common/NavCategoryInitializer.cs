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
        internal sealed class NavCategoryInitializer(
            ViewMode viewMode,
            int serializationId,
            string friendlyName,
            string nameResourceKey,
            string glyph,
            CategoryGroupType groupType,
            MyVirtualKey virtualKey,
            string? accessKey,
            bool supportsNegative)
        {
            internal ViewMode ViewMode { get; } = viewMode;

            internal int SerializationId { get; } = serializationId;

            internal string FriendlyName { get; } = friendlyName;

            internal string NameResourceKey { get; } = nameResourceKey;

            internal string Glyph { get; } = glyph;

            internal CategoryGroupType GroupType { get; } = groupType;

            internal MyVirtualKey VirtualKey { get; } = virtualKey;

            internal string? AccessKey { get; } = accessKey;

            internal bool SupportsNegative { get; } = supportsNegative;
        }
    }
}
