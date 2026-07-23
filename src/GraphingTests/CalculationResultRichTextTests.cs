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
            FontFamily = new FontFamily("Hind2"),
            MinFontSize = 14,
            MaxFontSize = 40,
            DisplayValue = "1,234.5",
            AdornmentText = "$",
            AdornmentFontFamily = new FontFamily("Noto Sans"),
            AdornmentSpacing = 1,
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
            Assert.Equal("$ 1,234.5", inlines.Text);
            Span adornment = Assert.IsType<Span>(inlines[0]);
            Span value = Assert.IsType<Span>(inlines[1]);
            Assert.Equal(result.AdornmentFontFamily, adornment.FontFamily);
            Assert.Equal(result.FontFamily, value.FontFamily);

            result.IsAdornmentOnRight = true;
            Assert.Equal("1,234.5 $", inlines.Text);
            Assert.Same(value, inlines[0]);
            Assert.Same(adornment, inlines[1]);

            var replacementValueFamily = new FontFamily("Replacement");
            result.FontFamily = replacementValueFamily;
            Assert.Equal(replacementValueFamily, value.FontFamily);

            result.DisplayValue = "2";
            result.AdornmentText = "m";
            result.AdornmentSpacing = 0;
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
