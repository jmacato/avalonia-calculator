// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

//#pragma once

//#include  "Common/Utils.h"

using System.ComponentModel;

namespace CalculatorApp
{
    namespace ViewModel
    {

        /// <summary>
        /// Model representation of a single item in the Memory list
        /// </summary>
        public partial class MemoryItemViewModel(StandardCalculatorViewModel calcVm) : INotifyPropertyChanged
        {
            // Private fields expanded from OBSERVABLE_PROPERTY_RW macros
            private int m_Position = -1;
            private string m_Value = string.Empty;

            // Field from constructor

            // Constructor

            // OBSERVABLE_OBJECT() expanded implementation
            public event PropertyChangedEventHandler? PropertyChanged;

            // Internal helper method from OBSERVABLE_OBJECT macro
            private void RaisePropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            // OBSERVABLE_PROPERTY_RW(int, Position) expansion
            public int Position
            {
                get => m_Position;
                set
                {
                    if (m_Position != value)
                    {
                        m_Position = value;
                        RaisePropertyChanged(nameof(Position));
                    }
                }
            }

            // OBSERVABLE_PROPERTY_RW(string, Value) expansion
            public string Value
            {
                get => m_Value;
                set
                {
                    if (m_Value != value)
                    {
                        m_Value = value;
                        RaisePropertyChanged(nameof(Value));
                    }
                }
            }

        };
    }
}
