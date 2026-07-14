// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp;

public sealed partial class Calculator : UserControl
{
    private StandardCalculatorViewModel? _subscribedModel;
    private HistoryList? _historyList;
    private Memory? _memory;
    private bool _isLastFlyoutHistory;
    private bool _isLastFlyoutMemory;
    private OpenFlyout _openFlyout;

    public Calculator()
    {
        InitializeComponent();
    }

    public StandardCalculatorViewModel? Model => DataContext as StandardCalculatorViewModel;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        SubscribeToModel();
        EnsureHistoryAndMemoryControls();
        ApplyResponsiveLayout();
        Focus();
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
            oldModel.PropertyChanged -= OnCalcPropertyChanged;
            oldModel.HistoryVM.HistoryItemClicked -= OnHistoryItemClicked;
            oldModel.HistoryVM.HideHistoryClicked -= OnHideHistoryClicked;
            oldModel.HideMemoryClicked -= OnHideMemoryClicked;
        }

        _subscribedModel = Model;
        if (_subscribedModel is not { } model)
        {
            if (_historyList is not null)
            {
                _historyList.DataContext = null;
            }

            if (_memory is not null)
            {
                _memory.DataContext = null;
            }

            return;
        }

        model.PropertyChanged += OnCalcPropertyChanged;
        model.HistoryVM.HistoryItemClicked += OnHistoryItemClicked;
        model.HistoryVM.HideHistoryClicked += OnHideHistoryClicked;
        model.HideMemoryClicked += OnHideMemoryClicked;
        EnsureHistoryAndMemoryControls();
        AutomationProperties.SetName(
            HistoryButton,
            AppResourceProvider.GetInstance().GetResourceString("HistoryButton_Open"));
        AutomationProperties.SetName(
            MemoryButton,
            AppResourceProvider.GetInstance().GetResourceString("MemoryButton_Open"));
        UpdateErrorState(model.IsInError);
    }

    private void OnHistoryItemClicked(HistoryItemViewModel item)
    {
        Model?.SelectHistoryItem(item);
        CloseFullScreenFlyout();
        Focus();
    }

    private void OnHideHistoryClicked() => CloseFullScreenFlyout();

    private void OnHideMemoryClicked() => CloseFullScreenFlyout();

    private void OnCalcPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == StandardCalculatorViewModel.IsInErrorPropertyName && Model is { } model)
        {
            UpdateErrorState(model.IsInError);
        }
        else if (e.PropertyName is nameof(StandardCalculatorViewModel.IsStandard)
                 or nameof(StandardCalculatorViewModel.IsScientific)
                 or nameof(StandardCalculatorViewModel.IsProgrammer)
                 or nameof(StandardCalculatorViewModel.IsMemoryEmpty))
        {
            ApplyResponsiveLayout();
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout();
        UpdateOpenFlyoutHeight();
    }

    private void EnsureHistoryAndMemoryControls()
    {
        _historyList ??= new HistoryList();
        _memory ??= new Memory();

        _historyList.DataContext = Model?.HistoryVM;
        _memory.DataContext = Model;
        _memory.IsErrorVisualState = Model?.IsInError == true;
    }

    private void ToggleHistoryFlyout(object? sender, RoutedEventArgs e)
    {
        if (Model is not { IsProgrammer: false } || DockPanel.IsVisible)
        {
            return;
        }

        if (_openFlyout == OpenFlyout.History)
        {
            CloseFullScreenFlyout();
            return;
        }

        OpenHistoryFlyout();
    }

    private void ToggleMemoryFlyout(object? sender, RoutedEventArgs e)
    {
        if (DockPanel.IsVisible)
        {
            return;
        }

        if (_openFlyout == OpenFlyout.Memory)
        {
            CloseFullScreenFlyout();
            return;
        }

        OpenMemoryFlyout();
    }

    private void OpenHistoryFlyout()
    {
        EnsureHistoryAndMemoryControls();
        CloseFullScreenFlyout(restoreFocus: false);
        DetachHistoryControl();

        _historyList!.SetDockedLayout(false);
        _historyList.RowHeight = new GridLength(NumpadPanel.Bounds.Height);
        HistoryFlyoutHolder.Content = _historyList;
        HistoryFlyoutHolder.IsVisible = true;
        MemoryFlyoutHolder.IsVisible = false;
        FullScreenFlyoutOverlay.IsVisible = true;
        _openFlyout = OpenFlyout.History;
        _isLastFlyoutHistory = true;
        _isLastFlyoutMemory = false;
        EnableCalculatorControls(false);
        AutomationProperties.SetName(
            HistoryButton,
            AppResourceProvider.GetInstance().GetResourceString("HistoryButton_Close"));
        _historyList.ScrollToBottom();
    }

    private void OpenMemoryFlyout()
    {
        EnsureHistoryAndMemoryControls();
        CloseFullScreenFlyout(restoreFocus: false);
        DetachMemoryControl();

        _memory!.SetDockedLayout(false);
        _memory.RowHeight = new GridLength(NumpadPanel.Bounds.Height);
        MemoryFlyoutHolder.Content = _memory;
        MemoryFlyoutHolder.IsVisible = true;
        HistoryFlyoutHolder.IsVisible = false;
        FullScreenFlyoutOverlay.IsVisible = true;
        _openFlyout = OpenFlyout.Memory;
        _isLastFlyoutHistory = false;
        _isLastFlyoutMemory = true;
        EnableCalculatorControls(false);
        AutomationProperties.SetName(
            MemoryButton,
            AppResourceProvider.GetInstance().GetResourceString("MemoryButton_Close"));
    }

    private void CloseFullScreenFlyout(bool restoreFocus = true)
    {
        OpenFlyout closing = _openFlyout;
        _openFlyout = OpenFlyout.None;

        if (ReferenceEquals(HistoryFlyoutHolder.Content, _historyList))
        {
            HistoryFlyoutHolder.Content = null;
        }

        if (ReferenceEquals(MemoryFlyoutHolder.Content, _memory))
        {
            MemoryFlyoutHolder.Content = null;
        }

        HistoryFlyoutHolder.IsVisible = false;
        MemoryFlyoutHolder.IsVisible = false;
        FullScreenFlyoutOverlay.IsVisible = false;
        EnableCalculatorControls(true);

        AutomationProperties.SetName(
            HistoryButton,
            AppResourceProvider.GetInstance().GetResourceString("HistoryButton_Open"));
        AutomationProperties.SetName(
            MemoryButton,
            AppResourceProvider.GetInstance().GetResourceString("MemoryButton_Open"));

        if (!restoreFocus)
        {
            return;
        }

        if (closing == OpenFlyout.History && HistoryButton.IsVisible && HistoryButton.IsEnabled)
        {
            HistoryButton.Focus();
        }
        else if (closing == OpenFlyout.Memory && MemoryButton.IsVisible && MemoryButton.IsEnabled)
        {
            MemoryButton.Focus();
        }
    }

    private void OnFlyoutSmokePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        CloseFullScreenFlyout();
        e.Handled = true;
    }

    private void UpdateOpenFlyoutHeight()
    {
        GridLength numpadHeight = new(Math.Max(0, NumpadPanel.Bounds.Height));
        if (_openFlyout == OpenFlyout.History && _historyList is not null)
        {
            _historyList.RowHeight = numpadHeight;
        }
        else if (_openFlyout == OpenFlyout.Memory && _memory is not null)
        {
            _memory.RowHeight = numpadHeight;
        }
    }

    private void EnableCalculatorControls(bool enable)
    {
        OpsPanel.IsEnabled = enable;
        MemoryPanel.IsEnabled = enable;
        if (enable && Model is { } model)
        {
            UpdateErrorState(model.IsInError);
        }
    }

    private void AttachHistoryToDock()
    {
        EnsureHistoryAndMemoryControls();
        DetachHistoryControl();
        _historyList!.SetDockedLayout(true);
        DockHistoryHolder.Child = _historyList;
    }

    private void AttachMemoryToDock()
    {
        EnsureHistoryAndMemoryControls();
        DetachMemoryControl();
        _memory!.SetDockedLayout(true);
        DockMemoryHolder.Child = _memory;
    }

    private void DetachHistoryControl()
    {
        if (ReferenceEquals(DockHistoryHolder.Child, _historyList))
        {
            DockHistoryHolder.Child = null;
        }

        if (ReferenceEquals(HistoryFlyoutHolder.Content, _historyList))
        {
            HistoryFlyoutHolder.Content = null;
        }
    }

    private void DetachMemoryControl()
    {
        if (ReferenceEquals(DockMemoryHolder.Child, _memory))
        {
            DockMemoryHolder.Child = null;
        }

        if (ReferenceEquals(MemoryFlyoutHolder.Content, _memory))
        {
            MemoryFlyoutHolder.Content = null;
        }
    }

    /// <summary>
    /// Avalonia does not expose WinUI AdaptiveTrigger. Keep the original
    /// Calculator.xaml state table here, using this view's client bounds so it
    /// behaves identically in desktop and WebAssembly hosts.
    /// </summary>
    private void ApplyResponsiveLayout()
    {
        if (Model is not { } model || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        double width = Bounds.Width;
        double height = Bounds.Height;

        bool fixedHistoryWidth = (width >= 1024 && height >= 768)
                                 || (width >= 768 && height >= 1366);
        bool dockVisible = width >= 560;

        DockPanel.IsVisible = dockVisible;
        if (fixedHistoryWidth)
        {
            LayoutRoot.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            LayoutRoot.ColumnDefinitions[1].Width = new GridLength(320);
        }
        else if (dockVisible)
        {
            // These are the exact 320*:240* WinUI state values. The history
            // column retains the original 320px maximum.
            LayoutRoot.ColumnDefinitions[0].Width = new GridLength(320, GridUnitType.Star);
            LayoutRoot.ColumnDefinitions[1].Width = new GridLength(240, GridUnitType.Star);
        }
        else
        {
            LayoutRoot.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            LayoutRoot.ColumnDefinitions[1].Width = new GridLength(0);
        }

        bool programmer = model.IsProgrammer;
        bool scientific = model.IsScientific;

        if (programmer)
        {
            model.IsDecimalEnabled = false;
            CalculatorPanel.RowDefinitions[3].Height = new GridLength(96, GridUnitType.Star);
            CalculatorPanel.RowDefinitions[3].MinHeight = 96;
            CalculatorPanel.RowDefinitions[5].Height = new GridLength(268, GridUnitType.Star);
        }
        else if (scientific)
        {
            model.IsDecimalEnabled = true;
            CalculatorPanel.RowDefinitions[3].Height = new GridLength(32, GridUnitType.Star);
            CalculatorPanel.RowDefinitions[3].MinHeight = 32;
            CalculatorPanel.RowDefinitions[5].Height = new GridLength(276, GridUnitType.Star);
        }
        else
        {
            model.IsDecimalEnabled = true;
            CalculatorPanel.RowDefinitions[3].Height = new GridLength(0);
            CalculatorPanel.RowDefinitions[3].MinHeight = 0;
            CalculatorPanel.RowDefinitions[5].Height = new GridLength(308, GridUnitType.Star);
        }

        // The WinUI ResultsM trigger is mode-specific: Standard=1,
        // Scientific=544, Programmer=640. ResultsL always begins at 800.
        double mediumResultThreshold = programmer ? 640 : scientific ? 544 : 1;
        if (height >= 800)
        {
            Results.MaxFontSize = 72;
            CalculatorPanel.RowDefinitions[2].MinHeight = 108;
            CalculatorPanel.RowDefinitions[2].Height = new GridLength(72, GridUnitType.Star);
        }
        else if (height >= mediumResultThreshold)
        {
            Results.MaxFontSize = 46;
            CalculatorPanel.RowDefinitions[2].MinHeight = 72;
            CalculatorPanel.RowDefinitions[2].Height = new GridLength(72, GridUnitType.Star);
        }
        else
        {
            Results.MaxFontSize = 26;
            CalculatorPanel.RowDefinitions[2].MinHeight = 42;
            CalculatorPanel.RowDefinitions[2].Height = new GridLength(42, GridUnitType.Star);
        }

        ClearMemoryButton.IsVisible = !programmer;
        MemRecall.IsVisible = !programmer;
        MemPlus.IsVisible = !programmer;
        MemMinus.IsVisible = !programmer;
        MemoryPanel.ColumnDefinitions[4].Width = programmer
            ? new GridLength(0.01, GridUnitType.Star)
            : new GridLength(1, GridUnitType.Star);
        MemoryPanel.ColumnDefinitions[5].Width = programmer
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0.01, GridUnitType.Star);
        MemoryPanel.ColumnDefinitions[6].Width = dockVisible
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(MemButton, programmer ? 5 : 4);
        Grid.SetColumn(MemoryButton, 6);

        HistoryButton.IsVisible = !programmer && !dockVisible;
        MemoryButton.IsVisible = !dockVisible;
        HistoryTab.IsVisible = !programmer;

        if (dockVisible)
        {
            CloseFullScreenFlyout(restoreFocus: false);
            if (programmer)
            {
                if (ReferenceEquals(DockHistoryHolder.Child, _historyList))
                {
                    DockHistoryHolder.Child = null;
                }
            }
            else
            {
                AttachHistoryToDock();
            }

            AttachMemoryToDock();

            if (programmer || _isLastFlyoutMemory)
            {
                DockTabs.SelectedItem = MemoryTab;
            }
            else if (_isLastFlyoutHistory || DockTabs.SelectedItem is null)
            {
                DockTabs.SelectedItem = HistoryTab;
            }
        }
        else
        {
            if (ReferenceEquals(DockHistoryHolder.Child, _historyList))
            {
                DockHistoryHolder.Child = null;
            }

            if (ReferenceEquals(DockMemoryHolder.Child, _memory))
            {
                DockMemoryHolder.Child = null;
            }
        }

        bool canRecall = !model.IsMemoryEmpty && !model.IsInError;
        ClearMemoryButton.IsEnabled = canRecall;
        MemRecall.IsEnabled = canRecall;
    }

    private void UpdateErrorState(bool isError)
    {
        OpsPanel.IsErrorVisualState = isError;
        ScientificAngleButtons.IsErrorVisualState = isError;
        ProgrammerDisplayPanel.IsErrorVisualState = isError;
        if (_memory is not null)
        {
            _memory.IsErrorVisualState = isError;
        }
        MemPlus.IsEnabled = !isError;
        MemMinus.IsEnabled = !isError;
        MemButton.IsEnabled = !isError;
        if (Model is { } model)
        {
            bool canRecall = !model.IsMemoryEmpty && !isError;
            ClearMemoryButton.IsEnabled = canRecall;
            MemRecall.IsEnabled = canRecall;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_openFlyout != OpenFlyout.None && e.Key == Key.Escape)
        {
            CloseFullScreenFlyout();
            e.Handled = true;
            return;
        }

        if (Model is not { } model)
        {
            return;
        }

        bool commandModifier = OperatingSystem.IsMacOS()
            ? e.KeyModifiers.HasFlag(KeyModifiers.Meta)
            : e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (commandModifier && e.Key == Key.C)
        {
            model.CopyCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (commandModifier && e.Key == Key.V)
        {
            model.PasteCommand.Execute(null);
            e.Handled = true;
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
            Key.Add => NumbersAndOperatorsEnum.Add,
            Key.Subtract => NumbersAndOperatorsEnum.Subtract,
            Key.Multiply => NumbersAndOperatorsEnum.Multiply,
            Key.Divide => NumbersAndOperatorsEnum.Divide,
            Key.Decimal or Key.OemPeriod or Key.OemComma => NumbersAndOperatorsEnum.Decimal,
            Key.Back => NumbersAndOperatorsEnum.Backspace,
            Key.Escape => NumbersAndOperatorsEnum.Clear,
            Key.Enter => NumbersAndOperatorsEnum.Equals,
            _ => NumbersAndOperatorsEnum.None
        };

        if (operation != NumbersAndOperatorsEnum.None)
        {
            model.ButtonPressed.Execute(new CalculatorButtonPressedEventArgs(string.Empty, operation));
            e.Handled = true;
        }
    }

    private void OnDockSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // TabControl raises its initial selection while XAML is still
        // populating the named PivotItem translations.
        if (DockTabs is null || HistoryTab is null || MemoryTab is null)
        {
            return;
        }

        if (ReferenceEquals(DockTabs.SelectedItem, MemoryTab))
        {
            _isLastFlyoutMemory = true;
            _isLastFlyoutHistory = false;
        }
        else if (ReferenceEquals(DockTabs.SelectedItem, HistoryTab))
        {
            _isLastFlyoutMemory = false;
            _isLastFlyoutHistory = true;
        }
    }

    private enum OpenFlyout
    {
        None,
        History,
        Memory,
    }
}
