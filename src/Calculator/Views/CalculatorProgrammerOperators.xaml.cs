// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp;

public sealed partial class CalculatorProgrammerOperators : UserControl
{
    public CalculatorProgrammerOperators()
    {
        InitializeComponent();
    }

    public StandardCalculatorViewModel? Model => DataContext as StandardCalculatorViewModel;

    internal void SetRadixButton(NumberBase numberBase)
    {
        HexButton.IsChecked = numberBase == NumberBase.HexBase;
        DecimalButton.IsChecked = numberBase == NumberBase.DecBase;
        OctButton.IsChecked = numberBase == NumberBase.OctBase;
        BinaryButton.IsChecked = numberBase == NumberBase.BinBase;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (Model is not { } model)
        {
            return;
        }

        model.PropertyChanged -= OnModelPropertyChanged;
        model.PropertyChanged += OnModelPropertyChanged;
        SetRadixButton(model.CurrentRadixType);
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StandardCalculatorViewModel.CurrentRadixType) && Model is { } model)
        {
            SetRadixButton(model.CurrentRadixType);
        }
    }

    private void DecButtonChecked(object? sender, RoutedEventArgs e) =>
        SwitchBase(NumberBase.DecBase, NumbersAndOperatorsEnum.DecButton);

    private void HexButtonChecked(object? sender, RoutedEventArgs e) =>
        SwitchBase(NumberBase.HexBase, NumbersAndOperatorsEnum.HexButton);

    private void BinButtonChecked(object? sender, RoutedEventArgs e) =>
        SwitchBase(NumberBase.BinBase, NumbersAndOperatorsEnum.BinButton);

    private void OctButtonChecked(object? sender, RoutedEventArgs e) =>
        SwitchBase(NumberBase.OctBase, NumbersAndOperatorsEnum.OctButton);

    private void SwitchBase(NumberBase numberBase, NumbersAndOperatorsEnum operation)
    {
        TraceLogger.GetInstance().UpdateButtonUsage(operation, ViewMode.Programmer);
        Model?.SwitchProgrammerModeBase(numberBase);
    }
}
