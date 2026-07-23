// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel;

namespace CalculatorApp;

public sealed partial class MemoryListItem : UserControl
{
    public static readonly StyledProperty<MemoryItemViewModel?> ModelProperty =
        AvaloniaProperty.Register<MemoryListItem, MemoryItemViewModel?>(nameof(Model));

    public MemoryListItem()
    {
        InitializeComponent();
    }

    public MemoryItemViewModel? Model
    {
        get => GetValue(ModelProperty) ?? DataContext as MemoryItemViewModel;
        set => SetValue(ModelProperty, value);
    }

    private void OnMemoryAddButtonClicked(object? sender, RoutedEventArgs e) => Model?.MemoryAdd();

    private void OnClearButtonClicked(object? sender, RoutedEventArgs e) => Model?.Clear();

    private void OnMemorySubtractButtonClicked(object? sender, RoutedEventArgs e) => Model?.MemorySubtract();

    private void OnClearSwipeInvoked(object? sender, SwipeItemInvokedEventArgs e) => Model?.Clear();

    private void OnMemoryAddSwipeInvoked(object? sender, SwipeItemInvokedEventArgs e) => Model?.MemoryAdd();

    private void OnMemorySubtractSwipeInvoked(object? sender, SwipeItemInvokedEventArgs e) => Model?.MemorySubtract();
}
