// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using CalculatorApp.ViewModel;

namespace CalculatorApp;

public sealed partial class OperatorsPanel : UserControl
{
    private CalculatorScientificOperators? _scientificOperators;
    private CalculatorProgrammerBitFlipPanel? _bitFlipPanel;
    private CalculatorProgrammerRadixOperators? _programmerRadixOperators;

    public static readonly StyledProperty<bool> IsBitFlipCheckedProperty =
        AvaloniaProperty.Register<OperatorsPanel, bool>(nameof(IsBitFlipChecked));

    public static readonly StyledProperty<bool> IsErrorVisualStateProperty =
        AvaloniaProperty.Register<OperatorsPanel, bool>(nameof(IsErrorVisualState));

    static OperatorsPanel()
    {
        IsBitFlipCheckedProperty.Changed.AddClassHandler<OperatorsPanel>(
            static (sender, args) => sender.OnIsBitFlipCheckedPropertyChanged(args.NewValue is true));
        IsErrorVisualStateProperty.Changed.AddClassHandler<OperatorsPanel>(
            static (sender, args) => sender.OnIsErrorVisualStatePropertyChanged(args.NewValue is true));
    }

    public OperatorsPanel()
    {
        InitializeComponent();
        if (IsBitFlipChecked)
        {
            EnsureProgrammerBitFlipPanel();
        }
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
        if (_scientificOperators is not null)
        {
            _scientificOperators.IsErrorVisualState = newValue;
        }

        if (_programmerRadixOperators is not null)
        {
            _programmerRadixOperators.IsErrorVisualState = newValue;
        }
    }

    public void EnsureScientificOps()
    {
        if (_scientificOperators is not null)
        {
            return;
        }

        _scientificOperators = new CalculatorScientificOperators();
        ScientificOperatorsHost.Children.Add(_scientificOperators);
        _scientificOperators.IsErrorVisualState = IsErrorVisualState;
    }

    public void EnsureProgrammerRadixOps()
    {
        if (_programmerRadixOperators is not null)
        {
            return;
        }

        _programmerRadixOperators = new CalculatorProgrammerRadixOperators();
        ProgrammerRadixOperatorsHost.Children.Add(_programmerRadixOperators);
        _programmerRadixOperators.IsErrorVisualState = IsErrorVisualState;
    }

    private void EnsureProgrammerBitFlipPanel()
    {
        if (_bitFlipPanel is not null)
        {
            return;
        }

        _bitFlipPanel = new CalculatorProgrammerBitFlipPanel();
        BitFlipPanelHost.Children.Add(_bitFlipPanel);
    }
}
