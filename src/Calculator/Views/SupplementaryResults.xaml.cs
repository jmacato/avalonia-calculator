// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using CalculatorApp.ViewModel;

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
