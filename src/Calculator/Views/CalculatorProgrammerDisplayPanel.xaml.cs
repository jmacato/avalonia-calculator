// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Interactivity;
using System.ComponentModel;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp;

public sealed partial class CalculatorProgrammerDisplayPanel : UserControl
{
    private bool _isErrorVisualState;
    private StandardCalculatorViewModel? _subscribedModel;

    public CalculatorProgrammerDisplayPanel()
    {
        InitializeComponent();
    }

    public StandardCalculatorViewModel? Model => DataContext as StandardCalculatorViewModel;

    public bool IsErrorVisualState
    {
        get => _isErrorVisualState;
        set
        {
            _isErrorVisualState = value;
            QwordButton.IsEnabled = !value;
            DwordButton.IsEnabled = !value;
            WordButton.IsEnabled = !value;
            ByteButton.IsEnabled = !value;
        }
    }

    private void OnBitLengthButtonPressed(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: string buttonId } || Model is not { } model)
        {
            return;
        }

        QwordButton.IsVisible = false;
        DwordButton.IsVisible = false;
        WordButton.IsVisible = false;
        ByteButton.IsVisible = false;

        switch (buttonId)
        {
            case "0":
                model.ValueBitLength = BitLength.ThirtyTwoBits;
                DwordButton.IsVisible = true;
                DwordButton.Focus();
                break;
            case "1":
                model.ValueBitLength = BitLength.SixteenBits;
                WordButton.IsVisible = true;
                WordButton.Focus();
                break;
            case "2":
                model.ValueBitLength = BitLength.EightBits;
                ByteButton.IsVisible = true;
                ByteButton.Focus();
                break;
            case "3":
                model.ValueBitLength = BitLength.SixtyFourBits;
                QwordButton.IsVisible = true;
                QwordButton.Focus();
                break;
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        SetSubscribedModel(Model);
        if (_subscribedModel is not { } model)
        {
            return;
        }

        UpdateInputMode(model.IsBitFlipChecked);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e) => SetSubscribedModel(null);

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

    private void OnInputModeClicked(object? sender, RoutedEventArgs e)
    {
        if (Model is { } model)
        {
            model.IsBitFlipChecked = ReferenceEquals(sender, BitFlip);
        }
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == StandardCalculatorViewModel.IsBitFlipCheckedPropertyName && Model is { } model)
        {
            UpdateInputMode(model.IsBitFlipChecked);
        }
    }

    private void UpdateInputMode(bool bitFlip)
    {
        FullKeypad.IsChecked = !bitFlip;
        BitFlip.IsChecked = bitFlip;
    }
}
