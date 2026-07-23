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

using System.ComponentModel;
using System.Windows.Input;
using ViewModeType = CalculatorApp.ViewModel.Common.ViewMode;

namespace CalculatorApp.ViewModel
{
    namespace Common
    {
        public partial class NavCategory(
            string name,
            string automationName,
            string glyph,
            string accessKey,
            ViewModeType viewMode,
            bool supportsNegative,
            bool isEnabled)
            : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;
            internal void RaisePropertyChanged(string p)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
            }

            private string m_Name = name;
            public string Name
            {
                get => m_Name;

                private set => m_Name = value;
            }

            private string m_AutomationName = automationName;
            public string AutomationName
            {
                get => m_AutomationName;

                private set => m_AutomationName = value;
            }

            private string m_Glyph = glyph;
            public string Glyph
            {
                get => m_Glyph;

                private set => m_Glyph = value;
            }

            private ViewModeType m_ViewMode = viewMode;
            public ViewModeType ViewMode
            {
                get => m_ViewMode;

                private set => m_ViewMode = value;
            }

            private string m_AccessKey = accessKey;
            public string AccessKey
            {
                get => m_AccessKey;

                private set => m_AccessKey = value;
            }

            private bool m_SupportsNegative = supportsNegative;
            public bool SupportsNegative
            {
                get => m_SupportsNegative;

                private set => m_SupportsNegative = value;
            }

            private bool m_IsEnabled = isEnabled;
            public bool IsEnabled
            {
                get => m_IsEnabled;

                set => m_IsEnabled = value;
            }

            private bool m_IsSelected;
            public bool IsSelected
            {
                get => m_IsSelected;
                set
                {
                    if (m_IsSelected == value)
                    {
                        return;
                    }

                    m_IsSelected = value;
                    RaisePropertyChanged(nameof(IsSelected));
                }
            }

            public ICommand? NavigationCommand { get; internal set; }

            public string AutomationId => m_ViewMode.ToString();

        };
    }
}
