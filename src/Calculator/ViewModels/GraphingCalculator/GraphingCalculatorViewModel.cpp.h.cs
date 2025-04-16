// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #pragma once

// #include "../Common/Utils.h"
// #include "EquationViewModel.cpp.h.cs"
// #include "VariableViewModel.h"
using CalculatorApp.ViewModel.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml.Data;
using  GraphControl;

namespace CalculatorApp.ViewModel
{
    [Microsoft.UI.Xaml.Data.Bindable]
    public sealed partial class GraphingCalculatorViewModel : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        internal void RaisePropertyChanged(string p)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        }

        public void UpdateVariables(IDictionary<string, Variable> variables)
        {
            throw new NotImplementedException();
        }

        public bool IsDecimalEnabled
        {
            get
            {
                return m_IsDecimalEnabled;
            }

            private set
            {
                if (m_IsDecimalEnabled != value)
                {
                    m_IsDecimalEnabled = value;
                    RaisePropertyChanged(nameof(IsDecimalEnabled));
                }
            }
        }

        private bool m_IsDecimalEnabled;

        public ObservableCollection<EquationViewModel> Equations
        {
            get
            {
                return m_Equations;
            }

            private set
            {
                if (m_Equations != value)
                {
                    m_Equations = value;
                    RaisePropertyChanged(nameof(Equations));
                }
            }
        }

        private ObservableCollection<EquationViewModel> m_Equations;

        public ObservableCollection<VariableViewModel> Variables
        {
            get
            {
                return m_Variables;
            }

            private set
            {
                if (m_Variables != value)
                {
                    m_Variables = value;
                    RaisePropertyChanged(nameof(Variables));
                }
            }
        }

        private ObservableCollection<VariableViewModel> m_Variables;

        public EquationViewModel SelectedEquation
        {
            get
            {
                return m_SelectedEquation;
            }

            private set
            {
                if (m_SelectedEquation != value)
                {
                    m_SelectedEquation = value;
                    RaisePropertyChanged(nameof(SelectedEquation));
                }
            }
        }

        private EquationViewModel m_SelectedEquation;

        public ICommand ButtonPressed
        {
            get
            {
                if (donotuse_ButtonPressed == null)
                {
                    donotuse_ButtonPressed = new DelegateCommand(OnButtonPressed);
                }
                return donotuse_ButtonPressed;
            }
        }

        private ICommand donotuse_ButtonPressed;

        public event EventHandler<VariableChangedEventArgs> VariableUpdated;

    }
}
