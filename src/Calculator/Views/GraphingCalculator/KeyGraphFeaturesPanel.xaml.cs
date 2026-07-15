// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CalculatorApp;

public sealed partial class KeyGraphFeaturesPanel : UserControl
{
    public KeyGraphFeaturesPanel() => InitializeComponent();

    public event EventHandler<RoutedEventArgs>? KeyGraphFeaturesClosed;

    private void OnCloseClicked(object? sender, RoutedEventArgs e) =>
        KeyGraphFeaturesClosed?.Invoke(this, e);
}
