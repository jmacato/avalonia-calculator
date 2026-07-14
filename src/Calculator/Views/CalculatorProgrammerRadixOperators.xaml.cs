// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Interactivity;
using CalculatorApp.ViewModel;

namespace CalculatorApp;

public sealed partial class CalculatorProgrammerRadixOperators : UserControl
{
    private enum BitShiftMode
    {
        Arithmetic,
        Logical,
        RotateCircular,
        RotateCarry
    }

    private bool _isErrorVisualState;

    public CalculatorProgrammerRadixOperators()
    {
        InitializeComponent();
        SetBitShiftMode(BitShiftMode.Arithmetic);
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

    private void OpenParenthesisButton_GotFocus(object? sender, RoutedEventArgs e)
    {
        Model?.SetOpenParenthesisCountNarratorAnnouncement();
    }

    private void FlyoutButton_Clicked(object? sender, RoutedEventArgs e)
    {
        BitwiseButton.FlyoutMenu?.Hide();
    }

    private void BitshiftFlyout_Checked(object? sender, RoutedEventArgs e)
    {
        var mode = sender switch
        {
            RadioButton button when ReferenceEquals(button, LogicalShiftButton) => BitShiftMode.Logical,
            RadioButton button when ReferenceEquals(button, RotateCircularButton) => BitShiftMode.RotateCircular,
            RadioButton button when ReferenceEquals(button, RotateCarryShiftButton) => BitShiftMode.RotateCarry,
            _ => BitShiftMode.Arithmetic
        };

        SetBitShiftMode(mode);
        string resourceKey = mode switch
        {
            BitShiftMode.Logical => "logicalShiftButtonSelected",
            BitShiftMode.RotateCircular => "rotateCircularButtonSelected",
            BitShiftMode.RotateCarry => "rotateCarryShiftButtonSelected",
            _ => "arithmeticShiftButtonSelected"
        };
        Model?.SetBitshiftRadioButtonCheckedAnnouncement(
            ViewModel.Common.AppResourceProvider.GetInstance().GetResourceString(resourceKey));
        BitShiftButton.FlyoutMenu?.Hide();
    }

    private void SetBitShiftMode(BitShiftMode mode)
    {
        LshButton.IsVisible = mode == BitShiftMode.Arithmetic;
        RshButton.IsVisible = mode == BitShiftMode.Arithmetic;
        LshLogicalButton.IsVisible = mode == BitShiftMode.Logical;
        RshLogicalButton.IsVisible = mode == BitShiftMode.Logical;
        RolButton.IsVisible = mode == BitShiftMode.RotateCircular;
        RorButton.IsVisible = mode == BitShiftMode.RotateCircular;
        RolCarryButton.IsVisible = mode == BitShiftMode.RotateCarry;
        RorCarryButton.IsVisible = mode == BitShiftMode.RotateCarry;
    }

    private void SetControlsEnabled(bool enabled)
    {
        Control[] controls =
        [
            BitwiseButton, BitShiftButton, AndButton, OrButton, NotButton, NandButton, NorButton, XorButton,
            LshButton, RshButton, LshLogicalButton, RshLogicalButton, RolButton, RorButton, RolCarryButton, RorCarryButton,
            OpenParenthesisButton, CloseParenthesisButton, ModButton, DivideButton, MultiplyButton, MinusButton, PlusButton,
            NegateButton, AButton, BButton, CButton, DButton, EButton, FButton
        ];
        foreach (Control control in controls)
        {
            control.IsEnabled = enabled;
        }
    }
}
