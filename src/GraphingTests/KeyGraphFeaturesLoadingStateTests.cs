using Avalonia.Headless.XUnit;
using Avalonia.Media;
using CalculatorApp.ViewModel;
using GraphControl;

namespace GraphingTests;

public sealed class KeyGraphFeaturesLoadingStateTests
{
    [AvaloniaFact(Timeout = 5_000)]
    public void FreshViewModelPublishesAPlaceholderForEveryResultRow()
    {
        using var viewModel = CreateViewModel();

        Assert.True(viewModel.IsCalculating);
        Assert.False(viewModel.AnalysisErrorVisible);
        Assert.True(viewModel.AnalysisItemsVisible);
        Assert.Equal(13, viewModel.KeyGraphFeaturesItems.Count);
        Assert.All(
            viewModel.KeyGraphFeaturesItems,
            item =>
            {
                Assert.True(item.IsText);
                Assert.Equal("Calculating...", Assert.Single(item.DisplayItems));
                Assert.Empty(item.GridItems);
            });
    }

    [AvaloniaFact(Timeout = 5_000)]
    public void CompletedAnalysisReplacesEveryPlaceholder()
    {
        using var viewModel = CreateViewModel();
        using var grapher = new Grapher();
        var equation = new Equation { Expression = "sin(x)" };
        KeyGraphFeaturesInfo analysis = Assert.IsType<KeyGraphFeaturesInfo>(
            grapher.AnalyzeEquation(equation));

        viewModel.ApplyAnalysis(analysis);

        Assert.False(viewModel.IsCalculating);
        Assert.False(viewModel.AnalysisErrorVisible);
        Assert.DoesNotContain(
            viewModel.KeyGraphFeaturesItems.SelectMany(item => item.DisplayItems),
            value => value == "Calculating...");
        KeyGraphFeaturesItem domain = Assert.Single(
            viewModel.KeyGraphFeaturesItems,
            item => item.Title == "Domain");
        Assert.Equal("x ∈ ℝ", Assert.Single(domain.DisplayItems));
    }

    [AvaloniaFact(Timeout = 5_000)]
    public void DisposedViewModelRejectsLateAnalysisResults()
    {
        var viewModel = CreateViewModel();
        using var grapher = new Grapher();
        var equation = new Equation { Expression = "sin(x)" };
        KeyGraphFeaturesInfo analysis = Assert.IsType<KeyGraphFeaturesInfo>(
            grapher.AnalyzeEquation(equation));

        viewModel.Dispose();
        viewModel.ApplyAnalysis(analysis);

        Assert.False(viewModel.IsCalculating);
        Assert.Empty(viewModel.KeyGraphFeaturesItems);
        Assert.Empty(viewModel.Expression);
        Assert.Empty(viewModel.FunctionLabelText);
        Assert.Null(viewModel.LineBrush);
    }

    private static KeyGraphFeaturesViewModel CreateViewModel()
    {
        var equation = new Equation { Expression = "sin(x)" };
        var source = new EquationViewModel(equation, 1, Colors.Blue, 0);
        return new KeyGraphFeaturesViewModel(source);
    }
}
