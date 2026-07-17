// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Avalonia.Controls;
using Avalonia.Interactivity;
using CalculatorApp.ViewModel;

namespace CalculatorApp;

public sealed partial class CalculatorProgrammerRadixOperators : UserControl
{
    private bool _isErrorVisualState;
    public CalculatorProgrammerRadixOperators()
    {
        InitializeComponent();
        SetBitShiftMode(CalculatorProgrammerRadixOperatorsBitShiftMode.Arithmetic);
    }

    public StandardCalculatorViewModel? Model => DataContext as StandardCalculatorViewModel;

    public bool IsErrorVisualState
    {
        get => _isErrorVisualState;
        set
        {
            if (_isErrorVisualState == value)
            {
                return;
            }

            _isErrorVisualState = value;
            SetControlsEnabled(!value);
            NumberPad.IsErrorVisualState = value;
        }
    }

    private void FlyoutButton_Clicked(object? sender, RoutedEventArgs e)
    {
        BitwiseButton.FlyoutMenu?.Hide();
    }

    private void BitshiftFlyout_Checked(object? sender, RoutedEventArgs e)
    {
        var mode = sender switch
        {
            RadioButton button when ReferenceEquals(button, LogicalShiftButton) => CalculatorProgrammerRadixOperatorsBitShiftMode.Logical,
            RadioButton button when ReferenceEquals(button, RotateCircularButton) => CalculatorProgrammerRadixOperatorsBitShiftMode.RotateCircular,
            RadioButton button when ReferenceEquals(button, RotateCarryShiftButton) => CalculatorProgrammerRadixOperatorsBitShiftMode.RotateCarry,
            _ => CalculatorProgrammerRadixOperatorsBitShiftMode.Arithmetic
        };
        SetBitShiftMode(mode);
        string resourceKey = mode switch
        {
            CalculatorProgrammerRadixOperatorsBitShiftMode.Logical => "logicalShiftButtonSelected",
            CalculatorProgrammerRadixOperatorsBitShiftMode.RotateCircular => "rotateCircularButtonSelected",
            CalculatorProgrammerRadixOperatorsBitShiftMode.RotateCarry => "rotateCarryShiftButtonSelected",
            _ => "arithmeticShiftButtonSelected"
        };
        Model?.SetBitshiftRadioButtonCheckedAnnouncement(ViewModel.Common.AppResourceProvider.Instance.GetResourceString(resourceKey));
        BitShiftButton.FlyoutMenu?.Hide();
    }

    private void SetBitShiftMode(CalculatorProgrammerRadixOperatorsBitShiftMode mode)
    {
        LshButton.IsVisible = mode == CalculatorProgrammerRadixOperatorsBitShiftMode.Arithmetic;
        RshButton.IsVisible = mode == CalculatorProgrammerRadixOperatorsBitShiftMode.Arithmetic;
        LshLogicalButton.IsVisible = mode == CalculatorProgrammerRadixOperatorsBitShiftMode.Logical;
        RshLogicalButton.IsVisible = mode == CalculatorProgrammerRadixOperatorsBitShiftMode.Logical;
        RolButton.IsVisible = mode == CalculatorProgrammerRadixOperatorsBitShiftMode.RotateCircular;
        RorButton.IsVisible = mode == CalculatorProgrammerRadixOperatorsBitShiftMode.RotateCircular;
        RolCarryButton.IsVisible = mode == CalculatorProgrammerRadixOperatorsBitShiftMode.RotateCarry;
        RorCarryButton.IsVisible = mode == CalculatorProgrammerRadixOperatorsBitShiftMode.RotateCarry;
    }

    private void SetControlsEnabled(bool enabled)
    {
        Control[] controls = [BitwiseButton, BitShiftButton, AndButton, OrButton, NotButton, NandButton, NorButton, XorButton, LshButton, RshButton, LshLogicalButton, RshLogicalButton, RolButton, RorButton, RolCarryButton, RorCarryButton, OpenParenthesisButton, CloseParenthesisButton, ModButton, DivideButton, MultiplyButton, MinusButton, PlusButton, NegateButton, AButton, BButton, CButton, DButton, EButton, FButton];
        foreach (Control control in controls)
        {
            control.IsEnabled = enabled;
        }
    }
}
