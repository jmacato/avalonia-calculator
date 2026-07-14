// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using CalculatorApp.ViewModel;

namespace CalculatorApp;

public sealed partial class OperatorsPanel : UserControl
{
    public static readonly StyledProperty<bool> IsBitFlipCheckedProperty =
        AvaloniaProperty.Register<OperatorsPanel, bool>(nameof(IsBitFlipChecked));

    public static readonly StyledProperty<bool> IsErrorVisualStateProperty =
        AvaloniaProperty.Register<OperatorsPanel, bool>(nameof(IsErrorVisualState));

    public OperatorsPanel()
    {
        InitializeComponent();
        IsBitFlipCheckedProperty.Changed.AddClassHandler<OperatorsPanel>(
            static (sender, args) => sender.OnIsBitFlipCheckedPropertyChanged(args.NewValue is true));
        IsErrorVisualStateProperty.Changed.AddClassHandler<OperatorsPanel>(
            static (sender, args) => sender.OnIsErrorVisualStatePropertyChanged(args.NewValue is true));
    }

    public StandardCalculatorViewModel? Model => DataContext as StandardCalculatorViewModel;

    public bool IsBitFlipChecked
    {
        get => GetValue(IsBitFlipCheckedProperty);
        set => SetValue(IsBitFlipCheckedProperty, value);
    }

    public bool IsErrorVisualState
    {
        get => GetValue(IsErrorVisualStateProperty);
        set => SetValue(IsErrorVisualStateProperty, value);
    }

    private void OnIsBitFlipCheckedPropertyChanged(bool newValue)
    {
        if (newValue)
        {
            EnsureProgrammerBitFlipPanel();
        }
    }

    private void OnIsErrorVisualStatePropertyChanged(bool newValue)
    {
        StandardOperators.IsErrorVisualState = newValue;
        ScientificOperators.IsErrorVisualState = newValue;
        ProgrammerRadixOperators.IsErrorVisualState = newValue;
    }

    public void EnsureScientificOps()
    {
    }

    public void EnsureProgrammerRadixOps()
    {
    }

    private void EnsureProgrammerBitFlipPanel()
    {
        BitFlipPanel.IsVisible = Model?.IsBinaryBitFlippingEnabled == true;
    }
}
