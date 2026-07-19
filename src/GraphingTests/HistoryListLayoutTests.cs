using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CalculatorApp;
using CalculatorApp.ViewModel;

namespace GraphingTests;

public sealed class HistoryListLayoutTests
{
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
}
