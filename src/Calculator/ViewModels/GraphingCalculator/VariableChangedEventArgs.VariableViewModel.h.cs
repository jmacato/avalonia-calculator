// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.ComponentModel;
using CalculatorApp.ViewModel.Common;
using GraphControl;

namespace CalculatorApp.ViewModel;

public sealed class VariableChangedEventArgs(string variableName, double newValue) : EventArgs
{
    public string VariableName { get; } = variableName;
    public double NewValue { get; } = newValue;
}
