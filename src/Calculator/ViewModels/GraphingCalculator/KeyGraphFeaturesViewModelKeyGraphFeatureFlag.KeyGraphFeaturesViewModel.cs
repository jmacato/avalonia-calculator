// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using CalculatorApp.ViewModel.Common;
using GraphControl;

namespace CalculatorApp.ViewModel;

[Flags]
internal enum KeyGraphFeaturesViewModelKeyGraphFeatureFlag
{
    Domain = 1,
    Range = 2,
    Parity = 4,
    Periodicity = 8,
    Zeros = 16,
    YIntercept = 32,
    Minima = 64,
    Maxima = 128,
    InflectionPoints = 256,
    VerticalAsymptotes = 512,
    HorizontalAsymptotes = 1024,
    ObliqueAsymptotes = 2048,
    MonotoneIntervals = 4096
}
