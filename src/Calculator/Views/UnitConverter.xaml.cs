// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp;

public sealed partial class UnitConverter : UserControl
{
    private UnitConverterViewModel? _subscribedModel;

    public UnitConverter()
    {
        InitializeComponent();
    }

    public UnitConverterViewModel? Model => DataContext as UnitConverterViewModel;

    public void SetDefaultFocus() => Value1.Focus();

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        SubscribeToModel();
        ApplyResponsiveLayout();
        SetDefaultFocus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        SubscribeToModel();
        ApplyResponsiveLayout();
    }

    private void SubscribeToModel()
    {
        if (ReferenceEquals(_subscribedModel, Model))
        {
            return;
        }

        if (_subscribedModel is { } oldModel)
        {
            oldModel.PropertyChanged -= OnModelPropertyChanged;
        }

        _subscribedModel = Model;
        if (_subscribedModel is { } model)
        {
            model.PropertyChanged += OnModelPropertyChanged;
            UpdateActiveValueState();
        }
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UnitConverterViewModel.Value1Active)
            or nameof(UnitConverterViewModel.Value2Active))
        {
            UpdateActiveValueState();
        }
    }

    private void UpdateActiveValueState()
    {
        Value1.UpdateTextState();
        Value2.UpdateTextState();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => ApplyResponsiveLayout();

    /// <summary>
    /// Direct equivalent of UnitConverter.xaml's AspectRatioTrigger and sizing
    /// VisualStates. Landscape begins when width is equal to or greater than
    /// height, exactly as in the WinUI source.
    /// </summary>
    private void ApplyResponsiveLayout()
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        bool landscape = Bounds.Width >= Bounds.Height;
        var columns = UnitConverterRootGrid.ColumnDefinitions;
        var rows = UnitConverterRootGrid.RowDefinitions;

        columns[0].Width = new GridLength(0);
        columns[1].Width = new GridLength(1, GridUnitType.Star);
        columns[2].Width = landscape
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        columns[3].Width = new GridLength(0);

        if (landscape)
        {
            rows[1].Height = new GridLength(4, GridUnitType.Star);
            rows[2].Height = new GridLength(2, GridUnitType.Star);
            rows[3].Height = new GridLength(4, GridUnitType.Star);
            rows[4].Height = new GridLength(2, GridUnitType.Star);
            rows[5].Height = new GridLength(2, GridUnitType.Star);
            rows[6].MinHeight = 0;
            rows[6].Height = new GridLength(0);

            Grid.SetRow(ConverterNumPad, 1);
            Grid.SetRowSpan(ConverterNumPad, 5);
            Grid.SetColumn(ConverterNumPad, 2);
            Grid.SetColumnSpan(ConverterNumPad, 2);
            SupplementaryResults.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        }
        else
        {
            rows[1].Height = new GridLength(56, GridUnitType.Star);
            rows[2].Height = new GridLength(32, GridUnitType.Star);
            rows[3].Height = new GridLength(56, GridUnitType.Star);
            rows[4].Height = new GridLength(32, GridUnitType.Star);
            rows[5].Height = GridLength.Auto;
            rows[6].MinHeight = 0;
            rows[6].Height = new GridLength(272, GridUnitType.Star);

            Grid.SetRow(ConverterNumPad, 6);
            Grid.SetRowSpan(ConverterNumPad, 1);
            Grid.SetColumn(ConverterNumPad, 1);
            Grid.SetColumnSpan(ConverterNumPad, 1);
            SupplementaryResults.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        }

        bool wide = Bounds.Width >= 640;
        bool extraWide = Bounds.Width >= 1280 && Bounds.Height >= 768;
        double currencyFontSize = wide ? 32 : 20;
        double unitHeight = wide ? 44 : 32;
        double commandFontSize = extraWide ? 24 : wide ? 20 : 14;
        double numberFontSize = extraWide ? 46 : wide ? 28 : 18;

        Value1.MaxFontSize = wide ? 46 : 40;
        Value2.MaxFontSize = wide ? 46 : 40;
        Value1.DisplayMargin = wide ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 0, 4);
        Value2.DisplayMargin = wide ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 0, 4);
        CurrencySymbol1Block.FontSize = currencyFontSize;
        CurrencySymbol2Block.FontSize = currencyFontSize;
        CurrencySymbol1Block.Margin = wide ? new Thickness(0, 0, 0, 17) : new Thickness(0, 0, 0, 8);
        CurrencySymbol2Block.Margin = wide ? new Thickness(0, 0, 0, 17) : new Thickness(0, 0, 0, 8);
        Units1.Height = unitHeight;
        Units2.Height = unitHeight;
        ClearEntryButtonPos0.FontSize = commandFontSize;
        BackSpaceButtonSmall.FontSize = commandFontSize;
        ConverterNegateButton.FontSize = extraWide ? 24 : wide ? 20 : 16;
        NumberPad.SetButtonFontSize(numberFontSize);
    }

    private void OnSupplementaryResultsPanelSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // Retained from the original WinUI handler. The epsilon prevents a
        // SizeChanged feedback loop caused by floating-point layout rounding.
        UnitConverterRootGrid.RowDefinitions[5].MinHeight = Math.Max(48, e.NewSize.Height + 0.01);
    }

    private void OnValueSelected(object sender)
    {
        if (sender is CalculationResult value)
        {
            value.UpdateTextState();
            value.IsActive = true;
        }
    }

    private void OnDropDownOpened(object? sender, EventArgs e)
    {
        if (Model is { } model)
        {
            model.IsDropDownOpen = true;
        }
    }

    private void OnDropDownClosed(object? sender, EventArgs e)
    {
        if (Model is { } model)
        {
            model.IsDropDownOpen = false;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (Model is not { } model)
        {
            return;
        }

        NumbersAndOperatorsEnum operation = e.Key switch
        {
            Key.D0 or Key.NumPad0 => NumbersAndOperatorsEnum.Zero,
            Key.D1 or Key.NumPad1 => NumbersAndOperatorsEnum.One,
            Key.D2 or Key.NumPad2 => NumbersAndOperatorsEnum.Two,
            Key.D3 or Key.NumPad3 => NumbersAndOperatorsEnum.Three,
            Key.D4 or Key.NumPad4 => NumbersAndOperatorsEnum.Four,
            Key.D5 or Key.NumPad5 => NumbersAndOperatorsEnum.Five,
            Key.D6 or Key.NumPad6 => NumbersAndOperatorsEnum.Six,
            Key.D7 or Key.NumPad7 => NumbersAndOperatorsEnum.Seven,
            Key.D8 or Key.NumPad8 => NumbersAndOperatorsEnum.Eight,
            Key.D9 or Key.NumPad9 => NumbersAndOperatorsEnum.Nine,
            Key.Decimal or Key.OemPeriod or Key.OemComma => NumbersAndOperatorsEnum.Decimal,
            Key.Back => NumbersAndOperatorsEnum.Backspace,
            Key.Delete or Key.Escape => NumbersAndOperatorsEnum.Clear,
            Key.Subtract or Key.OemMinus => NumbersAndOperatorsEnum.Negate,
            _ => NumbersAndOperatorsEnum.None
        };

        if (operation != NumbersAndOperatorsEnum.None)
        {
            model.ButtonPressed.Execute(operation);
            e.Handled = true;
        }
    }
}
