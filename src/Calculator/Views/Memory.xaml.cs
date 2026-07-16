// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CalculatorApp.ViewModel;

namespace CalculatorApp;

public sealed partial class Memory : UserControl
{
    public static readonly StyledProperty<GridLength> RowHeightProperty =
        AvaloniaProperty.Register<Memory, GridLength>(
            nameof(RowHeight),
            default);

    private bool _isErrorVisualState;
    private StandardCalculatorViewModel? _subscribedModel;

    public Memory()
    {
        InitializeComponent();
    }

    public StandardCalculatorViewModel? Model => DataContext as StandardCalculatorViewModel;

    public GridLength RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    public bool IsErrorVisualState
    {
        get => _isErrorVisualState;
        set
        {
            if (_isErrorVisualState == value)
            {
                return;
            }

            _isErrorVisualState = value;
            MemoryListView.IsEnabled = !value;
        }
    }

    /// <summary>
    /// Applies the original Memory DockedLayout/DefaultLayout VisualState
    /// setters when Calculator reparents the single Memory instance.
    /// </summary>
    public void SetDockedLayout(bool isDocked)
    {
        Grid.SetRow(MemoryPanel, isDocked ? 0 : 1);
        Grid.SetRowSpan(MemoryPanel, isDocked ? 2 : 1);
        MemoryListView.Padding = isDocked ? default : new Thickness(0, 24, 0, 0);
        BackgroundShade.IsVisible = !isDocked;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        SubscribeToModel();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        SetSubscribedModel(null);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        SubscribeToModel();
    }

    private void SubscribeToModel() => SetSubscribedModel(Model);

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

        UpdateState();
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e) => UpdateState();

    private void UpdateState()
    {
        bool hasItems = Model is { IsMemoryEmpty: false };
        MemoryPaneEmpty.IsVisible = !hasItems;
        MemoryListView.IsVisible = hasItems;
        ClearMemory.IsVisible = hasItems;
    }

    private void MemoryListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (MemoryListView.SelectedItem is not null)
        {
            MemoryListView.SelectedItem = null;
        }
    }

    private void MemoryListItemTapped(object? sender, TappedEventArgs e)
    {
        if (GetItemFromEventSource(e.Source) is MemoryItemViewModel memorySlot && Model is { } model)
        {
            model.OnMemoryItemPressed(memorySlot.Position);
        }
    }

    private static object? GetItemFromEventSource(object? source)
    {
        if (source is not Visual visual)
        {
            return null;
        }

        ListBoxItem? container = visual as ListBoxItem
                                 ?? visual.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
        return container?.DataContext;
    }

    private static void OnClearMenuItemClicked(object? sender, RoutedEventArgs e) =>
        GetMemoryItem(sender)?.Clear();

    private static void OnMemoryAddMenuItemClicked(object? sender, RoutedEventArgs e) =>
        GetMemoryItem(sender)?.MemoryAdd();

    private static void OnMemorySubtractMenuItemClicked(object? sender, RoutedEventArgs e) =>
        GetMemoryItem(sender)?.MemorySubtract();

    private static MemoryItemViewModel? GetMemoryItem(object? sender) =>
        (sender as MenuItem)?.DataContext as MemoryItemViewModel;
}
