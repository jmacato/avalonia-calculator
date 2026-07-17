// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel.Common;
using GraphControl;

namespace CalculatorApp.ViewModel;

public enum GraphingInputAction
{
    Insert,
    Backspace,
    Clear,
    Submit
}
