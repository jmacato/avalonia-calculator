// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp.Controls;

public sealed class CalculatorButton : Button
{
    public static readonly StyledProperty<NumbersAndOperatorsEnum> ButtonIdProperty =
        AvaloniaProperty.Register<CalculatorButton, NumbersAndOperatorsEnum>(nameof(ButtonId));

    public static readonly StyledProperty<string> AuditoryFeedbackProperty =
        AvaloniaProperty.Register<CalculatorButton, string>(nameof(AuditoryFeedback), string.Empty);

    public static readonly StyledProperty<IBrush?> HoverBackgroundProperty =
        AvaloniaProperty.Register<CalculatorButton, IBrush?>(nameof(HoverBackground));

    public static readonly StyledProperty<IBrush?> HoverForegroundProperty =
        AvaloniaProperty.Register<CalculatorButton, IBrush?>(nameof(HoverForeground));

    public static readonly StyledProperty<IBrush?> PressBackgroundProperty =
        AvaloniaProperty.Register<CalculatorButton, IBrush?>(nameof(PressBackground));

    public static readonly StyledProperty<IBrush?> PressForegroundProperty =
        AvaloniaProperty.Register<CalculatorButton, IBrush?>(nameof(PressForeground));

    public static readonly StyledProperty<IBrush?> DisabledBackgroundProperty =
        AvaloniaProperty.Register<CalculatorButton, IBrush?>(nameof(DisabledBackground));

    public static readonly StyledProperty<IBrush?> DisabledForegroundProperty =
        AvaloniaProperty.Register<CalculatorButton, IBrush?>(nameof(DisabledForeground));

    public CalculatorButton()
    {
        UpdateCommandParameter();
    }

    public NumbersAndOperatorsEnum ButtonId
    {
        get => GetValue(ButtonIdProperty);
        set => SetValue(ButtonIdProperty, value);
    }

    public string AuditoryFeedback
    {
        get => GetValue(AuditoryFeedbackProperty);
        set => SetValue(AuditoryFeedbackProperty, value);
    }

    public IBrush? HoverBackground
    {
        get => GetValue(HoverBackgroundProperty);
        set => SetValue(HoverBackgroundProperty, value);
    }

    public IBrush? HoverForeground
    {
        get => GetValue(HoverForegroundProperty);
        set => SetValue(HoverForegroundProperty, value);
    }

    public IBrush? PressBackground
    {
        get => GetValue(PressBackgroundProperty);
        set => SetValue(PressBackgroundProperty, value);
    }

    public IBrush? PressForeground
    {
        get => GetValue(PressForegroundProperty);
        set => SetValue(PressForegroundProperty, value);
    }

    public IBrush? DisabledBackground
    {
        get => GetValue(DisabledBackgroundProperty);
        set => SetValue(DisabledBackgroundProperty, value);
    }

    public IBrush? DisabledForeground
    {
        get => GetValue(DisabledForegroundProperty);
        set => SetValue(DisabledForegroundProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ButtonIdProperty || change.Property == AuditoryFeedbackProperty)
        {
            UpdateCommandParameter();
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        // Preserve an explicit command from XAML (memory/history controls) while
        // retaining the original default ButtonPressed binding for keypad buttons.
        if (Command is null)
        {
            Command = DataContext switch
            {
                StandardCalculatorViewModel calculator => calculator.ButtonPressed,
                UnitConverterViewModel converter => converter.ButtonPressed,
                GraphingCalculatorViewModel graphing => graphing.ButtonPressed,
                _ => null
            };
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            base.OnKeyDown(e);
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            base.OnKeyUp(e);
        }
    }

    private void UpdateCommandParameter()
    {
        CommandParameter = new CalculatorButtonPressedEventArgs(AuditoryFeedback, ButtonId);
    }
}
