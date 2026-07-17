// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp;

public sealed partial class CalculatorProgrammerBitFlipPanel : UserControl
{
    private readonly FlipButtons[] _flipButtons;
    private bool _updatingCheckedStates;
    private StandardCalculatorViewModel? _subscribedModel;

    public CalculatorProgrammerBitFlipPanel()
    {
        InitializeComponent();
        _flipButtons = [Bit0, Bit1, Bit2, Bit3, Bit4, Bit5, Bit6, Bit7, Bit8, Bit9, Bit10, Bit11, Bit12, Bit13, Bit14, Bit15, Bit16, Bit17, Bit18, Bit19, Bit20, Bit21, Bit22, Bit23, Bit24, Bit25, Bit26, Bit27, Bit28, Bit29, Bit30, Bit31, Bit32, Bit33, Bit34, Bit35, Bit36, Bit37, Bit38, Bit39, Bit40, Bit41, Bit42, Bit43, Bit44, Bit45, Bit46, Bit47, Bit48, Bit49, Bit50, Bit51, Bit52, Bit53, Bit54, Bit55, Bit56, Bit57, Bit58, Bit59, Bit60, Bit61, Bit62, Bit63];
    }

    public StandardCalculatorViewModel? Model => DataContext as StandardCalculatorViewModel;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        SetSubscribedModel(Model);
        if (_subscribedModel is not { } model)
        {
            return;
        }

        UpdateCheckedStates(true);
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

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == StandardCalculatorViewModel.BinaryDigitsPropertyName
            || e.PropertyName == nameof(StandardCalculatorViewModel.ValueBitLength))
        {
            UpdateCheckedStates(false);
        }
        else if (e.PropertyName == StandardCalculatorViewModel.IsBitFlipCheckedPropertyName
                 || e.PropertyName == StandardCalculatorViewModel.IsProgrammerPropertyName)
        {
            if (Model is { IsBitFlipChecked: true, IsProgrammer: true })
            {
                UpdateAutomationPropertiesNames();
            }
        }
    }

    private void OnBitToggled(object? sender, RoutedEventArgs e)
    {
        if (_updatingCheckedStates
            || sender is not FlipButtons flipButton
            || Model is not { IsBitFlipChecked: true, IsProgrammer: true } model)
        {
            return;
        }

        int index = int.Parse((string)flipButton.Tag!, CultureInfo.InvariantCulture);
        AutomationProperties.SetName(
            flipButton,
            GenerateAutomationPropertiesName(index, flipButton.IsChecked == true));
        model.ButtonPressed.Execute(flipButton.ButtonId);
    }

    private void UpdateCheckedStates(bool updateAutomationPropertiesNames)
    {
        if (Model is not { } model)
        {
            return;
        }

        _updatingCheckedStates = true;
        int lastEnabledBit = GetIndexOfLastBit(model.ValueBitLength);
        for (int index = 0; index < _flipButtons.Length; index++)
        {
            bool value = index < model.BinaryDigits.Count && model.BinaryDigits[index];
            FlipButtons flipButton = _flipButtons[index];
            bool changed = flipButton.IsChecked != value;
            flipButton.IsChecked = value;
            flipButton.IsEnabled = index <= lastEnabledBit;
            if (updateAutomationPropertiesNames || changed)
            {
                AutomationProperties.SetName(
                    flipButton,
                    GenerateAutomationPropertiesName(index, value));
            }
        }

        _updatingCheckedStates = false;
    }

    private void UpdateAutomationPropertiesNames()
    {
        foreach (FlipButtons flipButton in _flipButtons)
        {
            int index = int.Parse((string)flipButton.Tag!, CultureInfo.InvariantCulture);
            AutomationProperties.SetName(
                flipButton,
                GenerateAutomationPropertiesName(index, flipButton.IsChecked == true));
        }
    }

    private string GenerateAutomationPropertiesName(int position, bool value)
    {
        var resources = AppResourceProvider.Instance;
        string bitPosition;
        if (position == 0)
        {
            bitPosition = resources.GetResourceString("LeastSignificantBit");
        }
        else if (Model is { } model && position == GetIndexOfLastBit(model.ValueBitLength))
        {
            bitPosition = resources.GetResourceString("MostSignificantBit");
        }
        else
        {
            string indexName = resources.GetResourceString(position.ToString(CultureInfo.InvariantCulture));
            bitPosition = LocalizationStringUtil.GetLocalizedString(
                resources.GetResourceString("BitPosition"),
                indexName);
        }

        return LocalizationStringUtil.GetLocalizedString(
            resources.GetResourceString("BitFlipItemAutomationName"),
            bitPosition,
            value ? "1" : "0");
    }

    private static int GetIndexOfLastBit(BitLength length) => length switch
    {
        BitLength.SixtyFourBits => 63,
        BitLength.ThirtyTwoBits => 31,
        BitLength.SixteenBits => 15,
        BitLength.EightBits => 7,
        _ => -1
    };
}
