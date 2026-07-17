// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel;
using UnitConversionManager;

namespace CalculatorApp;

public sealed partial class SupplementaryResults : UserControl
{
    public static readonly StyledProperty<IEnumerable<SupplementaryResult>?> ResultsProperty = AvaloniaProperty.Register<SupplementaryResults, IEnumerable<SupplementaryResult>?>(nameof(Results));
    public SupplementaryResults()
    {
        InitializeComponent();
    }

    public IEnumerable<SupplementaryResult>? Results { get => GetValue(ResultsProperty); set => SetValue(ResultsProperty, value); }
}
