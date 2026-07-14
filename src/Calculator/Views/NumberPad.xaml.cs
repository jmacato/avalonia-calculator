// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp;

public sealed partial class NumberPad : UserControl
{
    public static readonly StyledProperty<ControlTheme?> ButtonStyleProperty =
        AvaloniaProperty.Register<NumberPad, ControlTheme?>(nameof(ButtonStyle));

    public static readonly StyledProperty<NumberBase> CurrentRadixTypeProperty =
        AvaloniaProperty.Register<NumberPad, NumberBase>(nameof(CurrentRadixType), NumberBase.DecBase);

    public static readonly StyledProperty<bool> IsDecimalEnabledProperty =
        AvaloniaProperty.Register<NumberPad, bool>(nameof(IsDecimalEnabled), true);

    private bool _isErrorVisualState;

    public NumberPad()
    {
        InitializeComponent();

        LocalizationSettings localizationSettings = LocalizationSettings.GetInstance();
        DecimalSeparatorButton.Content = localizationSettings.GetDecimalSeparator();
        Num0Button.Content = localizationSettings.GetDigitSymbolFromEnUsDigit('0');
        Num1Button.Content = localizationSettings.GetDigitSymbolFromEnUsDigit('1');
        Num2Button.Content = localizationSettings.GetDigitSymbolFromEnUsDigit('2');
        Num3Button.Content = localizationSettings.GetDigitSymbolFromEnUsDigit('3');
        Num4Button.Content = localizationSettings.GetDigitSymbolFromEnUsDigit('4');
        Num5Button.Content = localizationSettings.GetDigitSymbolFromEnUsDigit('5');
        Num6Button.Content = localizationSettings.GetDigitSymbolFromEnUsDigit('6');
        Num7Button.Content = localizationSettings.GetDigitSymbolFromEnUsDigit('7');
        Num8Button.Content = localizationSettings.GetDigitSymbolFromEnUsDigit('8');
        Num9Button.Content = localizationSettings.GetDigitSymbolFromEnUsDigit('9');
    }

    public ControlTheme? ButtonStyle
    {
        get => GetValue(ButtonStyleProperty);
        set => SetValue(ButtonStyleProperty, value);
    }

    public NumberBase CurrentRadixType
    {
        get => GetValue(CurrentRadixTypeProperty);
        set => SetValue(CurrentRadixTypeProperty, value);
    }

    public bool IsDecimalEnabled
    {
        get => GetValue(IsDecimalEnabledProperty);
        set => SetValue(IsDecimalEnabledProperty, value);
    }

    public bool IsErrorVisualState
    {
        get => _isErrorVisualState;
        set
        {
            if (_isErrorVisualState != value)
            {
                _isErrorVisualState = value;
                DecimalSeparatorButton.IsEnabled = !value && IsDecimalEnabled;
            }
        }
    }

    public void SetButtonFontSize(double fontSize)
    {
        Num0Button.FontSize = fontSize;
        Num1Button.FontSize = fontSize;
        Num2Button.FontSize = fontSize;
        Num3Button.FontSize = fontSize;
        Num4Button.FontSize = fontSize;
        Num5Button.FontSize = fontSize;
        Num6Button.FontSize = fontSize;
        Num7Button.FontSize = fontSize;
        Num8Button.FontSize = fontSize;
        Num9Button.FontSize = fontSize;
        DecimalSeparatorButton.FontSize = fontSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CurrentRadixTypeProperty)
        {
            UpdateRadixButtons(change.GetNewValue<NumberBase>());
        }
        else if (change.Property == IsDecimalEnabledProperty)
        {
            DecimalSeparatorButton.IsEnabled = change.GetNewValue<bool>() && !IsErrorVisualState;
        }
    }

    private void UpdateRadixButtons(NumberBase numberBase)
    {
        CalculatorButton[] buttons =
        [
            Num0Button, Num1Button, Num2Button, Num3Button, Num4Button,
            Num5Button, Num6Button, Num7Button, Num8Button, Num9Button
        ];

        foreach (CalculatorButton button in buttons)
        {
            button.IsEnabled = true;
        }

        if (numberBase == NumberBase.BinBase)
        {
            for (int index = 2; index < buttons.Length; index++)
            {
                buttons[index].IsEnabled = false;
            }
        }
        else if (numberBase == NumberBase.OctBase)
        {
            Num8Button.IsEnabled = false;
            Num9Button.IsEnabled = false;
        }
    }
}
