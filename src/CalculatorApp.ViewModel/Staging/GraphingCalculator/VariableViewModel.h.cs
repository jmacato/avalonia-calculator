// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #pragma once

// #include "../Common/Utils.h"
// #include "CalcViewModel/Common/LocalizationStringUtil.h"
// #include "EquationViewModel.cpp.h.cs"

using GraphControl;
using System;
using System.ComponentModel;

namespace CalculatorApp.ViewModel
{

public struct VariableChangedEventArgs
{
    public string variableName;
    public double newValue;
}

[Windows.UI.Xaml.Data.Bindable]
public partial class VariableViewModel : INotifyPropertyChanged
{

    public const int DefaultMinMaxRange = 10;

    public VariableViewModel(string name, Variable variable)
    {
        m_Name = name;
        m_variable = variable;
        m_SliderSettingsVisible = false;
    }

    // Expanded from OBSERVABLE_OBJECT()
    public event PropertyChangedEventHandler PropertyChanged;

    internal void RaisePropertyChanged(string p)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    // Expanded from OBSERVABLE_PROPERTY_R(string ^, Name)
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
    private string m_Name;

    // Expanded from OBSERVABLE_PROPERTY_RW(bool, SliderSettingsVisible)
    public bool SliderSettingsVisible
    {
        get
        {
            return m_SliderSettingsVisible;
        }
        set
        {
            m_SliderSettingsVisible = value;
        }
    }
    private bool m_SliderSettingsVisible;

    public double Min
    {
        get
        {
            return m_variable.Min;
        }
        set
        {
            if (m_variable.Min != value)
            {
                if (value >= m_variable.Max)
                {
                    m_variable.Max = value + DefaultMinMaxRange;
                    RaisePropertyChanged("Max");
                }

                m_variable.Min = value;
                RaisePropertyChanged("Min");
            }
        }
    }

    public double Step
    {
        get
        {
            return m_variable.Step;
        }
        set
        {
            if (m_variable.Step != value)
            {
                m_variable.Step = value;
                RaisePropertyChanged("Step");
            }
        }
    }

    public double Max
    {
        get
        {
            return m_variable.Max;
        }
        set
        {
            if (m_variable.Max != value)
            {
                if (value <= m_variable.Min)
                {
                    m_variable.Min = value - DefaultMinMaxRange;
                    RaisePropertyChanged("Min");
                }

                m_variable.Max = value;
                RaisePropertyChanged("Max");
            }
        }
    }

    public event EventHandler<VariableChangedEventArgs> VariableUpdated;

    public double Value
    {
        get
        {
            return m_variable.Value;
        }
        set
        {
            if (value < m_variable.Min)
            {
                m_variable.Min = value;
                RaisePropertyChanged("Min");
            }
            else if (value > m_variable.Max)
            {
                m_variable.Max = value;
                RaisePropertyChanged("Max");
            }

            if (m_variable.Value != value)
            {
                m_variable.Value = value;
                VariableUpdated(this, new VariableChangedEventArgs { variableName = Name, newValue = value });
                RaisePropertyChanged("Value");
            }
        }
    }

    public string VariableAutomationName
    {
        get
        {
            return CalculatorApp.ViewModel.Common.LocalizationStringUtil.GetLocalizedString(
                CalculatorApp.ViewModel.Common.AppResourceProvider.GetInstance().GetResourceString("VariableListViewItem"), Name);
        }
    }

    private Variable m_variable;
}
}
