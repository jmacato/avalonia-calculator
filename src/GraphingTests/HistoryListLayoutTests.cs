using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CalculatorApp;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel;

namespace GraphingTests;

public sealed class HistoryListLayoutTests
{
    [AvaloniaFact(Timeout = 5_000)]
    public void RecycledSwipeRowsAreNotRootedBySharedSwipeItems()
    {
        var sharedItems = new SwipeItems
        {
            new SwipeItem()
        };
        WeakReference<SwipeControl> recycledRow = LoadThenUnloadSwipeRow(sharedItems);

        CollectGarbage();
        Assert.False(recycledRow.TryGetTarget(out _));
    }

    [AvaloniaFact(Timeout = 5_000)]
    public void NonEmptyHistoryCanRealizeAndMeasureItsFirstRow()
    {
        var model = new StandardCalculatorViewModel();
        model.HistoryVM.Items.Add(new HistoryItemViewModel(
            "1 + 2 =",
            "3",
            Array.Empty<(string, int)>(),
            Array.Empty<CalcEngine.IExpressionCommand>()));

        var history = new HistoryList
        {
            DataContext = model.HistoryVM,
            RowHeight = new GridLength(500)
        };
        history.SetDockedLayout(false);

        var window = new Window
        {
            Width = 393,
            Height = 741,
            Content = history
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.True(history.DesiredSize.Width > 0);
            Assert.True(history.DesiredSize.Height > 0);
        }
        finally
        {
            window.Close();
        }
    }

    private static WeakReference<SwipeControl> LoadThenUnloadSwipeRow(SwipeItems sharedItems)
    {
        var row = new SwipeControl
        {
            RightItems = sharedItems,
            Content = new Border()
        };
        var reference = new WeakReference<SwipeControl>(row);
        row.RaiseEvent(new RoutedEventArgs(Control.LoadedEvent));
        row.RaiseEvent(new RoutedEventArgs(Control.UnloadedEvent));
        return reference;
    }

    private static void CollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
