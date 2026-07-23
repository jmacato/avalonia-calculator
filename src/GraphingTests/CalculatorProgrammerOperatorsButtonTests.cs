using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CalculatorApp;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;

namespace GraphingTests;

public sealed class CalculatorProgrammerOperatorsButtonTests
{
    [AvaloniaFact]
    public void RadixSelectorsAreFullSurfaceButtonsThatSwitchTheActiveBase()
    {
        var model = new StandardCalculatorViewModel();
        var selectors = new CalculatorProgrammerOperators
        {
            DataContext = model
        };
        var window = new Window
        {
            Width = 440,
            Height = 132,
            Content = selectors
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button hexButton = FindButton(selectors, "HexButton");
            Button decimalButton = FindButton(selectors, "DecimalButton");
            Button octButton = FindButton(selectors, "OctButton");
            Button binaryButton = FindButton(selectors, "BinaryButton");

            Assert.IsType<Button>(hexButton);
            Assert.IsType<Button>(decimalButton);
            Assert.IsType<Button>(octButton);
            Assert.IsType<Button>(binaryButton);
            Assert.True(hexButton.Bounds.Width >= 400);
            Assert.Contains("selected", decimalButton.Classes);

            Click(hexButton);
            Assert.Equal(NumberBase.HexBase, model.CurrentRadixType);
            Assert.Contains("selected", hexButton.Classes);

            Click(octButton);
            Assert.Equal(NumberBase.OctBase, model.CurrentRadixType);
            Assert.Contains("selected", octButton.Classes);

            Click(binaryButton);
            Assert.Equal(NumberBase.BinBase, model.CurrentRadixType);
            Assert.Contains("selected", binaryButton.Classes);
        }
        finally
        {
            window.Close();
        }
    }

    private static Button FindButton(
        CalculatorProgrammerOperators selectors,
        string name)
    {
        return selectors.FindControl<Button>(name) ??
            throw new InvalidOperationException(
                $"Radix button '{name}' was not created.");
    }

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
}
