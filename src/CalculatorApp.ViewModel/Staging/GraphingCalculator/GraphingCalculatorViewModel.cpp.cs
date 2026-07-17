// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #include "pch.h"
// #include "GraphingCalculatorViewModel.cpp.h"

using CalculatorApp.ViewModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml.Data;
using GraphControl;
using System.Collections.Generic;

namespace CalculatorApp.ViewModel;

public partial class GraphingCalculatorViewModel
{
    public GraphingCalculatorViewModel()
    {

        m_IsDecimalEnabled = true;
        m_Equations = new();
        m_Variables = new();
    }

    void OnButtonPressed(object parameter)
    {
    }

    //void UpdateVariables(IMap<String, Variable>  variables)
    //{
    //    Variables.Clear();
    //    foreach(var variablePair in  variables)
    //    {
    //        var variable = new VariableViewModel(variablePair.Key, variablePair.Value);
    //        variable.VariableUpdated += new EventHandler<VariableChangedEventArgs>([this, variable](Object  sender, VariableChangedEventArgs e) {
    //            VariableUpdated(variable, VariableChangedEventArgs{ e.variableName, e.newValue });
    //        });
    //        Variables.Append(variable);
    //    }
    //}

    public void SetSelectedEquation(EquationViewModel equation)
    {
        SelectedEquation = equation;
    }
}
