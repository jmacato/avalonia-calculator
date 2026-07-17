// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Interactivity;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp;

public sealed partial class CalculatorScientificAngleButtons : UserControl
{
    private bool _isErrorVisualState;

    public CalculatorScientificAngleButtons()
    {
        InitializeComponent();
    }

    public StandardCalculatorViewModel? Model => DataContext as StandardCalculatorViewModel;

    public bool IsErrorVisualState
    {
        get => _isErrorVisualState;
        set
        {
            _isErrorVisualState = value;
            DegreeButton.IsEnabled = !value;
            RadianButton.IsEnabled = !value;
            GradsButton.IsEnabled = !value;
            FtoeButton.IsEnabled = !value && (Model?.IsFToEEnabled ?? true);
        }
    }

    private void OnAngleButtonPressed(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: string buttonId } || Model is not { } model)
        {
            return;
        }

        DegreeButton.IsVisible = false;
        RadianButton.IsVisible = false;
        GradsButton.IsVisible = false;
        switch (buttonId)
        {
            case "0":
                model.SwitchAngleType(CalculatorButtonId.Radians);
                RadianButton.IsVisible = true;
                RadianButton.Focus();
                break;
            case "1":
                model.SwitchAngleType(CalculatorButtonId.Grads);
                GradsButton.IsVisible = true;
                GradsButton.Focus();
                break;
            case "2":
                model.SwitchAngleType(CalculatorButtonId.Degree);
                DegreeButton.IsVisible = true;
                DegreeButton.Focus();
                break;
        }
    }

    private void FToEButton_Toggled(object? sender, RoutedEventArgs e)
    {
        Model?.FtoEButtonToggled();
    }
}
