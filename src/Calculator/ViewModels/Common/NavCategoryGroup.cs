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

namespace CalculatorApp.ViewModel
{
    namespace Common
    {
        public partial class NavCategoryGroup : INotifyPropertyChanged
        {
            // Group headers share the built-in ListBox item pipeline with the
            // selectable categories, but remain non-interactive.
            public bool IsEnabled { get; }

            public event PropertyChangedEventHandler? PropertyChanged;
            internal void RaisePropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            public string Name
            {
                get => m_Name;

                private set
                {
                    if (m_Name != value)
                    {
                        m_Name = value;
                        RaisePropertyChanged(nameof(Name));
                    }
                }
            }

            private string m_Name;
            public string AutomationName
            {
                get => m_AutomationName;

                private set
                {
                    if (m_AutomationName != value)
                    {
                        m_AutomationName = value;
                        RaisePropertyChanged(nameof(AutomationName));
                    }
                }
            }

            private string m_AutomationName;
            public CategoryGroupType GroupType
            {
                get => m_GroupType;

                private set
                {
                    if (m_GroupType != value)
                    {
                        m_GroupType = value;
                        RaisePropertyChanged(nameof(GroupType));
                    }
                }
            }

            private CategoryGroupType m_GroupType;
            public ObservableCollection<NavCategory> Categories
            {
                get => m_Categories;

                private set
                {
                    if (m_Categories != value)
                    {
                        m_Categories = value;
                        RaisePropertyChanged(nameof(Categories));
                    }
                }
            }

            private ObservableCollection<NavCategory> m_Categories;
        }
    }
}
