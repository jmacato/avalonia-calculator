// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;

namespace CalculatorApp;

public sealed partial class HistoryList : UserControl
{
    public static readonly StyledProperty<GridLength> RowHeightProperty =
        AvaloniaProperty.Register<HistoryList, GridLength>(nameof(RowHeight), default);

    private HistoryViewModel? _subscribedModel;

    public HistoryList()
    {
        InitializeComponent();
    }

    public HistoryViewModel? Model => DataContext as HistoryViewModel;

    public GridLength RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    public static string GetHistoryItemAutomationName(string accExpression, string accResult) =>
        $"{accExpression} {accResult}";

    public void ScrollToBottom()
    {
        if (HistoryListView.ItemCount > 0)
        {
            HistoryListView.ScrollIntoView(HistoryListView.ItemCount - 1);
        }
    }

    /// <summary>
    /// Applies the original HistoryList DockedLayout/DefaultLayout VisualState
    /// setters. Avalonia has no window-scoped AdaptiveTrigger, so Calculator
    /// drives the same state while it reparents this control between the
    /// original flyout and dock holders.
    /// </summary>
    public void SetDockedLayout(bool isDocked)
    {
        Grid.SetRow(HistoryListRootGrid, isDocked ? 0 : 1);
        Grid.SetRowSpan(HistoryListRootGrid, isDocked ? 2 : 1);
        HistoryListView.Padding = isDocked ? default : new Thickness(0, 24, 0, 0);
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

    private void SetSubscribedModel(HistoryViewModel? model)
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
        bool hasItems = Model?.Items.Count > 0;
        HistoryEmpty.IsVisible = !hasItems;
        HistoryListView.IsVisible = hasItems;
        ClearHistory.IsVisible = hasItems;
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // WinUI's original ListView uses SelectionMode=None and ItemClick.
        // Avalonia ListBox has no None mode, so discard its pointer-down
        // selection and perform the original completed-click action in Tapped.
        if (HistoryListView.SelectedItem is not null)
        {
            HistoryListView.SelectedItem = null;
        }
    }

    private void OnHistoryListTapped(object? sender, TappedEventArgs e)
    {
        if (GetItemFromEventSource(e.Source) is HistoryItemViewModel clickedItem && Model is { } model)
        {
            model.ShowItem(clickedItem);
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

    private static void OnCopyMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: HistoryItemViewModel item })
        {
            CopyPasteManager.CopyToClipboard(item.Result);
        }
    }

    private void OnDeleteMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: HistoryItemViewModel item })
        {
            Model?.DeleteItem(item);
        }
    }

    private void OnDeleteSwipeInvoked(SwipeItem sender, SwipeItemInvokedEventArgs e)
    {
        if (e.SwipeControl.DataContext is HistoryItemViewModel swipedItem)
        {
            Model?.DeleteItem(swipedItem);
        }
    }
}
