// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CalculatorApp;

public sealed partial class GraphingNumPad : UserControl
{
    public GraphingNumPad()
    {
        InitializeComponent();
    }

    private void ShiftButton_Check(object? sender, RoutedEventArgs e) =>
        SetOperatorRowVisibility();

    private void ShiftButton_Uncheck(object? sender, RoutedEventArgs e)
    {
        ShiftButton.IsChecked = false;
        SetOperatorRowVisibility();
    }

    private void TrigFlyoutShift_Toggle(object? sender, RoutedEventArgs e) =>
        SetTrigRowVisibility();

    private void TrigFlyoutHyp_Toggle(object? sender, RoutedEventArgs e) =>
        SetTrigRowVisibility();

    private void FlyoutButton_Clicked(object? sender, RoutedEventArgs e)
    {
        HypButton.IsChecked = false;
        TrigShiftButton.IsChecked = false;
        SetTrigRowVisibility();
        TrigButton.FlyoutMenu?.Hide();
        InequalityButton.FlyoutMenu?.Hide();
        FuncButton.FlyoutMenu?.Hide();
    }

    private void SetOperatorRowVisibility()
    {
        bool inverse = ShiftButton.IsChecked == true;
        Row1.IsVisible = !inverse;
        InvRow1.IsVisible = inverse;
    }

    private void SetTrigRowVisibility()
    {
        bool inverse = TrigShiftButton.IsChecked == true;
        bool hyperbolic = HypButton.IsChecked == true;
        TrigFunctions.IsVisible = !inverse && !hyperbolic;
        InverseTrigFunctions.IsVisible = inverse && !hyperbolic;
        HyperbolicTrigFunctions.IsVisible = !inverse && hyperbolic;
        InverseHyperbolicTrigFunctions.IsVisible = inverse && hyperbolic;
    }
}
