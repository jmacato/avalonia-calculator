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
    public sealed class VariableChangedEventArgs : EventArgs
    {
        public VariableChangedEventArgs(string variableName, double newValue)
        {
            VariableName = variableName;
            NewValue = newValue;
        }

        public string VariableName { get; }
        public double NewValue { get; }
    }
}
