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
using System.Windows.Input;
using ViewModeType = CalculatorApp.ViewModel.Common.ViewMode;

namespace CalculatorApp.ViewModel
{
    namespace Common
    {
        public partial class NavCategory : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;
            internal void RaisePropertyChanged(string p)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
            }

            private string m_Name;
            public string Name
            {
                get
                {
                    return m_Name;
                }

                private set
                {
                    m_Name = value;
                }
            }

            private string m_AutomationName;
            public string AutomationName
            {
                get
                {
                    return m_AutomationName;
                }

                private set
                {
                    m_AutomationName = value;
                }
            }

            private string m_Glyph;
            public string Glyph
            {
                get
                {
                    return m_Glyph;
                }

                private set
                {
                    m_Glyph = value;
                }
            }

            private ViewModeType m_ViewMode;
            public ViewModeType ViewMode
            {
                get
                {
                    return m_ViewMode;
                }

                private set
                {
                    m_ViewMode = value;
                }
            }

            private string m_AccessKey;
            public string AccessKey
            {
                get
                {
                    return m_AccessKey;
                }

                private set
                {
                    m_AccessKey = value;
                }
            }

            private bool m_SupportsNegative;
            public bool SupportsNegative
            {
                get
                {
                    return m_SupportsNegative;
                }

                private set
                {
                    m_SupportsNegative = value;
                }
            }

            private bool m_IsEnabled;
            public bool IsEnabled
            {
                get
                {
                    return m_IsEnabled;
                }

                set
                {
                    m_IsEnabled = value;
                }
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

            public string AutomationId
            {
                get
                {
                    return m_ViewMode.ToString();
                }
            }

            // static bool IsCalculatorViewMode(ViewModeType mode);
            // static bool IsGraphingCalculatorViewMode(ViewModeType mode);
            // static bool IsDateCalculatorViewMode(ViewModeType mode);
            // static bool IsConverterViewMode(ViewModeType mode);
            public NavCategory(string name, string automationName, string glyph, string accessKey, string mode, ViewModeType viewMode, bool supportsNegative, bool isEnabled)
            {
                m_Name = (name);
                m_AutomationName = (automationName);
                m_Glyph = (glyph);
                m_AccessKey = (accessKey);
                m_modeString = (mode);
                m_ViewMode = (viewMode);
                m_SupportsNegative = (supportsNegative);
                m_IsEnabled = (isEnabled);
            }

            // private:
            //     static bool IsModeInCategoryGroup(ViewModeType mode, CategoryGroupType groupType);
            string m_modeString;
        };
    }
}
