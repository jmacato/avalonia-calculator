// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Channels;
using System.Windows.Input;
using Avalonia.Threading;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using UnitConversionManager;

namespace CalculatorApp.ViewModel;

internal enum UnitConverterViewModelConversionParameter
{
    Source,
    Target
}
