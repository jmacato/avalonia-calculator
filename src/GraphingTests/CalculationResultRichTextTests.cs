using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using CalculatorApp.Controls;

namespace GraphingTests;

public sealed class CalculationResultRichTextTests
{
    [AvaloniaFact(Timeout = 5_000)]
    public void ValueAndAdornmentRemainSeparateSelectableSpans()
    {
        SelectableTextBlock? output = null;
        var result = new CalculationResult
        {
            Width = 320,
            Height = 80,
            MinFontSize = 14,
            MaxFontSize = 40,
            DisplayValue = "1,234.5",
            AdornmentText = "$",
            AdornmentSpacingEm = 2,
            Template = new FuncControlTemplate<CalculationResult>((_, scope) =>
            {
                output = new SelectableTextBlock
                {
                    Name = "NormalOutput"
                }.RegisterInNameScope(scope);
                return new ScrollViewer
                {
                    Name = "TextContainer",
                    Content = output
                }.RegisterInNameScope(scope);
            })
        };
        var window = new Window
        {
            Width = 400,
            Height = 120,
            Content = result
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            SelectableTextBlock richOutput = Assert.IsType<SelectableTextBlock>(output);
            InlineCollection inlines = Assert.IsType<InlineCollection>(richOutput.Inlines);
            Assert.Equal(2, inlines.Count);
            Assert.All(inlines, inline => Assert.IsType<Span>(inline));
            Assert.Equal("$\u2003\u20031,234.5", inlines.Text);

            result.IsAdornmentOnRight = true;
            Assert.Equal("1,234.5\u2003\u2003$", inlines.Text);

            result.DisplayValue = "2";
            result.AdornmentText = "m";
            result.AdornmentSpacingEm = 0;
            Assert.Equal("2m", inlines.Text);
            Assert.Equal("2", result.GetRawDisplayValue());
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void PhysicalAdornmentSideMapsToParagraphDirection()
    {
        Assert.True(CalculationResult.IsAdornmentFirst(FlowDirection.LeftToRight, false));
        Assert.False(CalculationResult.IsAdornmentFirst(FlowDirection.LeftToRight, true));
        Assert.False(CalculationResult.IsAdornmentFirst(FlowDirection.RightToLeft, false));
        Assert.True(CalculationResult.IsAdornmentFirst(FlowDirection.RightToLeft, true));
    }
}
