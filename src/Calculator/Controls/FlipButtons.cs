// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls.Primitives;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp.Controls;

public sealed class FlipButtons : ToggleButton
{
    public static readonly StyledProperty<CalculatorButtonId> ButtonIdProperty =
        AvaloniaProperty.Register<FlipButtons, CalculatorButtonId>(nameof(ButtonId));

    public FlipButtons()
    {
        Content = "0";
    }

    public CalculatorButtonId ButtonId
    {
        get => GetValue(ButtonIdProperty);
        set => SetValue(ButtonIdProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        System.ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == ButtonIdProperty)
        {
            CommandParameter = change.GetNewValue<CalculatorButtonId>();
        }
        else if (change.Property == IsCheckedProperty)
        {
            Content = change.GetNewValue<bool?>() == true ? "1" : "0";
        }
    }
}
