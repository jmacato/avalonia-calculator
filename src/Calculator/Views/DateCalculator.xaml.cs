// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia.Controls;
using Avalonia.Interactivity;
using CalculatorApp.ViewModel;

namespace CalculatorApp;

public sealed partial class DateCalculator : UserControl, IDisposable
{
    private int _disposed;

    public DateCalculator()
    {
        InitializeComponent();
    }

    public void CloseCalendarFlyout()
    {
        DateDiff_FromDate.IsDropDownOpen = false;
        DateDiff_ToDate.IsDropDownOpen = false;
        AddSubtract_FromDate.IsDropDownOpen = false;
    }

    public void SetDefaultFocus()
    {
        DateCalculationOption.Focus();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        bool leftAligned = Bounds.Width >= 480;
        DateCalculatorGrid.ColumnDefinitions[0].Width = leftAligned
            ? new GridLength(456)
            : new GridLength(1, GridUnitType.Star);
        DateCalculatorGrid.ColumnDefinitions[1].Width = leftAligned
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        DateDiffAllUnitsResultLabel.FontSize = leftAligned ? 20 : 14;
        DateResultLabel.FontSize = leftAligned ? 20 : 14;
    }

    private void OnCopyMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (DataContext is DateCalculatorViewModel viewModel)
        {
            viewModel.OnCopyCommand(null);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        CloseCalendarFlyout();
        DataContext = null;
        GC.SuppressFinalize(this);
    }
}
