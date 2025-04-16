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
        // Don't change the order of these enums
        // and definitely don't use int arithmetic
        // to change modes.
        public
            enum ViewMode
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

        public
            enum CategoryGroupType
        {
            None = -1,
            Calculator = 0,
            Converter = 1
        };

        public class NavCategoryInitializer
        {
            public ViewMode viewMode;
            public int serializationId;
            public string friendlyName;
            public string nameResourceKey;
            public string glyph;
            public CategoryGroupType groupType;
            public MyVirtualKey virtualKey;
            public string? accessKey;
            public bool supportsNegative;


            public NavCategoryInitializer(
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


                this.viewMode = viewMode;
                this.serializationId = serializationId;
                this.friendlyName = friendlyName;
                this.nameResourceKey = nameResourceKey;
                this.glyph = glyph;
                this.groupType = groupType;
                this.virtualKey = virtualKey;
                this.accessKey = accessKey;
                this.supportsNegative = supportsNegative;
            }
        };

        public class NavCategoryGroupInitializer
        {
            public NavCategoryGroupInitializer(CategoryGroupType t, string h, string n, string a)

            {
                type = (t);
                headerResourceKey = (h);
                modeResourceKey = (n);
                automationResourceKey = (a);
            }

            public CategoryGroupType type;
            public string headerResourceKey;
            public string modeResourceKey;
            public string automationResourceKey;
        };

        [Windows.UI.Xaml.Data.Bindable]
        public partial class NavCategory : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            internal void RaisePropertyChanged(string p)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
            }

            private string m_Name;

            public string Name
            {
                get { return m_Name; }
                private set { m_Name = value; }
            }

            private string m_AutomationName;

            public string AutomationName
            {
                get { return m_AutomationName; }
                private set { m_AutomationName = value; }
            }

            private string m_Glyph;

            public string Glyph
            {
                get { return m_Glyph; }
                private set { m_Glyph = value; }
            }

            private ViewModeType m_ViewMode;

            public ViewModeType ViewMode
            {
                get { return m_ViewMode; }
                private set { m_ViewMode = value; }
            }

            private string m_AccessKey;

            public string AccessKey
            {
                get { return m_AccessKey; }
                private set { m_AccessKey = value; }
            }

            private bool m_SupportsNegative;

            public bool SupportsNegative
            {
                get { return m_SupportsNegative; }
                private set { m_SupportsNegative = value; }
            }

            private bool m_IsEnabled;

            public bool IsEnabled
            {
                get { return m_IsEnabled; }
                set { m_IsEnabled = value; }
            }

            public string
                AutomationId
            {
                get { return m_ViewMode.ToString(); }
            }

            // static bool IsCalculatorViewMode(ViewModeType mode);
            // static bool IsGraphingCalculatorViewMode(ViewModeType mode);
            // static bool IsDateCalculatorViewMode(ViewModeType mode);
            // static bool IsConverterViewMode(ViewModeType mode);

            public NavCategory(
                string name,
                string automationName,
                string glyph,
                string accessKey,
                string mode,
                ViewModeType viewMode,
                bool supportsNegative,
                bool isEnabled)
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

        [Windows.UI.Xaml.Data.Bindable]
        public partial class NavCategoryGroup : INotifyPropertyChanged
        {

            public event PropertyChangedEventHandler PropertyChanged;

            internal void RaisePropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            public string Name
            {
                get { return m_Name; }

                private set
                {
                    if (m_Name != value)
                    {
                        m_Name = value;
                        RaisePropertyChanged("Name");
                    }
                }
            }

            private string m_Name;

            public string AutomationName
            {
                get { return m_AutomationName; }

                private set
                {
                    if (m_AutomationName != value)
                    {
                        m_AutomationName = value;
                        RaisePropertyChanged("AutomationName");
                    }
                }
            }

            private string m_AutomationName;

            public CategoryGroupType GroupType
            {
                get { return m_GroupType; }

                private set
                {
                    if (m_GroupType != value)
                    {
                        m_GroupType = value;
                        RaisePropertyChanged("GroupType");
                    }
                }
            }

            private CategoryGroupType m_GroupType;

            public ObservableCollection<NavCategory> Categories
            {
                get { return m_Categories; }

                private set
                {
                    if (m_Categories != value)
                    {
                        m_Categories = value;
                        RaisePropertyChanged("Categories");
                    }
                }
            }

            private ObservableCollection<NavCategory> m_Categories;
        }

        public partial class NavCategoryStates
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
