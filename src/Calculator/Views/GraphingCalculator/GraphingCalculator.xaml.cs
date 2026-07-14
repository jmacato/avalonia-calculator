// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Automation;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp;

public sealed partial class GraphingCalculator : UserControl
{
    private const double SmallStateWidth = 800;

    public GraphingCalculator()
    {
        InitializeComponent();
    }

    public void SetDefaultFocus() => EquationInputAreaControl.SetDefaultFocus();

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
        SetDefaultFocus();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => ApplyResponsiveLayout();

    private void OnSwitchModeChanged(object? sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
        UpdateSwitchModeAccessibility();
        if (SwitchModeToggleButton.IsChecked == true)
        {
            EquationInputAreaControl.SetDefaultFocus();
        }
        else
        {
            SwitchModeToggleButton.Focus();
        }
    }

    private void ApplyResponsiveLayout()
    {
        if (Bounds.Width <= 0)
        {
            return;
        }

        bool small = Bounds.Width < SmallStateWidth;
        bool equationMode = SwitchModeToggleButton.IsChecked == true;

        SwitchModeToggleButton.IsVisible = small;
        Grid.SetColumn(LeftGrid, 0);
        Grid.SetColumnSpan(LeftGrid, small ? 2 : 1);
        Grid.SetColumn(RightGrid, small ? 0 : 1);
        Grid.SetColumnSpan(RightGrid, small ? 2 : 1);

        // This is the original ShouldDisplayPanel truth table. Wide mode shows
        // both; narrow mode defaults to graph and the checked state shows the
        // equation pane.
        LeftGrid.IsVisible = !small || !equationMode;
        RightGrid.IsVisible = !small || equationMode;
        UpdateSwitchModeAccessibility();
    }

    private void UpdateSwitchModeAccessibility()
    {
        bool equationMode = SwitchModeToggleButton.IsChecked == true;
        string nextMode = AppResourceProvider.GetInstance().GetResourceString(
            equationMode ? "GraphSwitchToGraphMode" : "GraphSwitchToEquationMode");
        AutomationProperties.SetName(SwitchModeToggleButton, nextMode);
        ToolTip.SetTip(SwitchModeToggleButton, nextMode);
    }
}
