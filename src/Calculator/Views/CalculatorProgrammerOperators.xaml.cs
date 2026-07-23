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
    private StandardCalculatorViewModel? _subscribedModel;

    public CalculatorProgrammerOperators()
    {
        InitializeComponent();
    }

    public StandardCalculatorViewModel? Model => DataContext as StandardCalculatorViewModel;

    internal void SetRadixButton(NumberBase numberBase)
    {
        HexButton.Classes.Set("selected", numberBase == NumberBase.HexBase);
        DecimalButton.Classes.Set("selected", numberBase == NumberBase.DecBase);
        OctButton.Classes.Set("selected", numberBase == NumberBase.OctBase);
        BinaryButton.Classes.Set("selected", numberBase == NumberBase.BinBase);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        SetSubscribedModel(Model);
        if (_subscribedModel is not { } model)
        {
            return;
        }

        SetRadixButton(model.CurrentRadixType);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        SetSubscribedModel(null);
    }

    private void SetSubscribedModel(StandardCalculatorViewModel? model)
    {
        if (ReferenceEquals(_subscribedModel, model))
        {
            return;
        }

        if (_subscribedModel is not null)
        {
            _subscribedModel.PropertyChanged -= OnModelPropertyChanged;
        }

        _subscribedModel = model;
        if (_subscribedModel is not null)
        {
            _subscribedModel.PropertyChanged += OnModelPropertyChanged;
        }
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StandardCalculatorViewModel.CurrentRadixType) && Model is { } model)
        {
            SetRadixButton(model.CurrentRadixType);
        }
    }

    private void DecButtonChecked(object? sender, RoutedEventArgs e)
    {
        SwitchBase(NumberBase.DecBase, CalculatorButtonId.DecButton);
    }

    private void HexButtonChecked(object? sender, RoutedEventArgs e)
    {
        SwitchBase(NumberBase.HexBase, CalculatorButtonId.HexButton);
    }

    private void BinButtonChecked(object? sender, RoutedEventArgs e)
    {
        SwitchBase(NumberBase.BinBase, CalculatorButtonId.BinButton);
    }

    private void OctButtonChecked(object? sender, RoutedEventArgs e)
    {
        SwitchBase(NumberBase.OctBase, CalculatorButtonId.OctButton);
    }

    private void SwitchBase(NumberBase numberBase, CalculatorButtonId operation)
    {
        TraceLogger.Instance.UpdateButtonUsage(operation, ViewMode.Programmer);
        Model?.SwitchProgrammerModeBase(numberBase);
    }
}
