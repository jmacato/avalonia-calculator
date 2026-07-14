// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;

namespace CalculatorApp;

public sealed partial class CalculatorStandardOperators : UserControl
{
    private bool _isErrorVisualState;

    public CalculatorStandardOperators()
    {
        InitializeComponent();
    }

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
            PercentButton.IsEnabled = !value;
            SquareRootButton.IsEnabled = !value;
            XPower2Button.IsEnabled = !value;
            InvertButton.IsEnabled = !value;
            DivideButton.IsEnabled = !value;
            MultiplyButton.IsEnabled = !value;
            MinusButton.IsEnabled = !value;
            PlusButton.IsEnabled = !value;
            NegateButton.IsEnabled = !value;
            NumberPad.IsErrorVisualState = value;
        }
    }
}
