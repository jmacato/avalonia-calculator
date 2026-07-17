// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Globalization;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using GraphControl;
using Graphing;

namespace CalculatorApp.ViewModel;

public sealed class GraphLineWidthChoice
{
    public required double Width { get; init; }
    public required string AutomationName { get; init; }
}
