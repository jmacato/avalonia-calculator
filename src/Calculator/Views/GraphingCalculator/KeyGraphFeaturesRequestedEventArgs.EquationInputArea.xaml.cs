// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CalculatorApp.ViewModel;

namespace CalculatorApp;

public sealed class KeyGraphFeaturesRequestedEventArgs(EquationViewModel equation) : EventArgs
{
    public EquationViewModel Equation { get; } = equation;
}
