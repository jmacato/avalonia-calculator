using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using CalculatorApp.Controls;
using Graphing;
using GraphControl;
using MathComposer.Avalonia;
using MathComposer.Avalonia.Controls;
using MathComposer.Core;

namespace GraphingTests;

public sealed class GraphMathComposerIntegrationTests
{
    [AvaloniaFact(Timeout = 5_000)]
    public void MathControlsReuseTheSingleApplicationFontInitialization()
    {
        MathFontResources.Initialize();
        var firstEditor = new MathEditor();
        var secondEditor = new MathEditor();
        var display = new MathDisplay();

        firstEditor.Measure(Size.Infinity);
        secondEditor.Measure(Size.Infinity);
        display.Measure(Size.Infinity);

        Assert.Equal(1, MathFontResources.InitializationCount);
        Assert.Same(MathFontResources.LayoutEngine, MathFontResources.LayoutEngine);
        Assert.Same(MathFontResources.Renderer, MathFontResources.Renderer);
    }

    [Theory]
    [InlineData("arcsin(x)")]
    [InlineData("n₁ ∈ ℤ")]
    [InlineData("π/2")]
    [InlineData("sqrt(sin(x)×π)")]
    [InlineData("root(x, 3)")]
    [InlineData("floor(x) ≤ abs(x)")]
    public void GraphAnalysisValuesImportDirectlyAsMathMl(string expression)
    {
        MathParseResult parsed = MathInterchange.Parse(expression, MathTextFormat.UnicodeMath);
        string mathMl = MathMl(expression);
        MathParseResult reparsed = MathInterchange.Parse(mathMl, MathTextFormat.MathMl);

        Assert.Empty(parsed.Diagnostics);
        Assert.Empty(reparsed.Diagnostics);
        Assert.StartsWith("<math", mathMl, StringComparison.Ordinal);
        Assert.DoesNotContain("pi</", mathMl, StringComparison.Ordinal);
    }

    [Fact]
    public void UnicodeSubscriptDigitsBecomeStructuralMathScripts()
    {
        MathParseResult parsed = MathInterchange.Parse("n₁₂", MathTextFormat.UnicodeMath);

        MathScript script = Assert.IsType<MathScript>(Assert.Single(parsed.Document.Root.Children));
        Assert.Equal("n", Assert.IsType<MathText>(script.Base).Text);
        Assert.Equal("12", Assert.IsType<MathText>(Assert.Single(script.Subscript!.Children)).Text);
        Assert.Empty(parsed.Diagnostics);
    }

    [AvaloniaFact(Timeout = 5_000)]
    public void NativeMathDisplayRendersMathDocumentWithoutSerialization()
    {
        MathDocument document = MathInterchange.Parse(
            "root(x, 3) ≤ arcsin(x)",
            MathTextFormat.UnicodeMath).Document;
        var display = new MathDisplay
        {
            Document = document,
            MathFontSize = 18
        };
        var window = new Window
        {
            Width = 400,
            Height = 120,
            Content = display
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.True(display.DesiredSize.Width > 0);
            Assert.True(display.DesiredSize.Height > 0);
            Assert.Same(document, display.Document);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact(Timeout = 5_000)]
    public void GraphEquationEditorKeepsNativeMathMlAcrossSubmission()
    {
        string mathMl = MathMl("sqrt(sin(x)×π)");
        var editor = new MathRichEditBox
        {
            MathText = mathMl
        };
        MathRichEditBoxSubmissionEventArgs? submission = null;
        editor.EquationSubmitted += (_, args) => submission = args;

        editor.SubmitEquation(EquationSubmissionSource.Programmatic);

        Assert.NotNull(submission);
        Assert.False(submission.HasTextChanged);
        Assert.Equal(mathMl, editor.MathText);
        Assert.IsType<MathRadical>(Assert.Single(editor.Editor.Document.Root.Children));
        Assert.Contains("<mi>π</mi>", editor.MathText, StringComparison.Ordinal);
        Assert.DoesNotContain(">pi<", editor.MathText, StringComparison.Ordinal);
        Assert.False(editor.HasEquationError);
    }

    [AvaloniaFact(Timeout = 5_000)]
    public void GraphEquationEditorSynchronizesLiveMathMlAndStructuralBackspace()
    {
        var editor = new MathRichEditBox();
        editor.InsertText("sin()", 4, 0);

        MathFunction function = Assert.IsType<MathFunction>(
            Assert.Single(editor.Editor.Document.Root.Children));
        Assert.Equal("sin", function.Name);
        Assert.StartsWith("<math", editor.MathText, StringComparison.Ordinal);

        editor.Clear();
        Assert.Empty(editor.Editor.Document.Root.Children);
        Assert.Equal(string.Empty, editor.MathText);
    }

    [AvaloniaFact(Timeout = 5_000)]
    public void GraphEquationEditorAutoCorrectsTypedRelationsAndSymbols()
    {
        var editor = new MathRichEditBox();
        foreach (char character in "x<=pi+\\alpha+")
        {
            editor.Editor.InsertText(character.ToString());
        }

        Assert.Equal(
            "x≤π+α+",
            MathInterchange.Serialize(editor.Editor.Document, MathTextFormat.UnicodeMath));
        Assert.Contains(
            editor.Editor.Document.Root.Children,
            static node => node is MathText { Text: "≤", AtomClass: MathAtomClass.Relation });
        Assert.DoesNotContain(
            editor.Editor.Document.Root.Children,
            static node => node is MathText { Text: "pi" });
    }

    [AvaloniaFact(Timeout = 5_000)]
    public void MathEditorMeasuresStructuredContentAtItsNaturalHeight()
    {
        var editor = new MathEditor
        {
            MathFontSize = 22
        };
        editor.Load("x", MathTextFormat.UnicodeMath);
        editor.Measure(Size.Infinity);
        double textHeight = editor.DesiredSize.Height;

        editor.Load("x/2", MathTextFormat.UnicodeMath);
        editor.Measure(Size.Infinity);

        Assert.True(textHeight > 0);
        Assert.True(editor.DesiredSize.Height > textHeight);
    }

    [AvaloniaFact(Timeout = 5_000)]
    public void EmptyGraphEquationEditorUsesNativeMathWatermark()
    {
        var editor = new MathRichEditBox();

        Assert.IsType<MathDisplay>(editor.Watermark);
        Assert.Same(
            MathRichEditBox.FunctionEquationPlaceholderDocument,
            editor.Watermark.Document);
        Assert.Collection(
            editor.Watermark.Document.Root.Children,
            node => Assert.Equal(
                new MathText("f", MathAtomClass.Identifier),
                Assert.IsType<MathText>(node)),
            node =>
            {
                MathDelimiter delimiter = Assert.IsType<MathDelimiter>(node);
                Assert.Equal("(", delimiter.Opening);
                Assert.Equal(")", delimiter.Closing);
                Assert.Equal(
                    new MathText("x", MathAtomClass.Identifier),
                    Assert.IsType<MathText>(Assert.Single(delimiter.Body.Children)));
            },
            node => Assert.Equal(
                new MathText("=", MathAtomClass.Relation),
                Assert.IsType<MathText>(node)));
        Assert.True(editor.Watermark.IsVisible);
        Assert.Equal(Brushes.Black, editor.Editor.Foreground);
    }

    [AvaloniaFact(Timeout = 5_000)]
    public void GraphEquationTextWatermarkReplacesNativeMathWatermark()
    {
        var editor = new MathRichEditBox
        {
            PlaceholderText = "Enter an expression"
        };

        Assert.True(editor.TextWatermark.IsVisible);
        Assert.Equal("Enter an expression", editor.TextWatermark.Text);
        Assert.False(editor.Watermark.IsVisible);
    }

    [Fact]
    public void GraphSolverConsumesMathMlWithoutAnIntermediateEquationFormat()
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.ParsingOptions().SetFormatType(FormatType.MathML);
        string firstMathMl = MathMl("sin(x)×π");
        string secondMathMl = MathMl("sqrt(x)");

        IExpression first = solver.ParseInput(firstMathMl, out int firstError, out int firstErrorType)!;
        IExpression second = solver.ParseInput(secondMathMl, out int secondError, out int secondErrorType)!;
        IExpression combined = solver.CombineExpressions([first, second]);
        IReadOnlyList<IEquation>? equations = solver.CreateGrapher().TryInitialize(combined);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(0, firstError);
        Assert.Equal(0, firstErrorType);
        Assert.Equal(0, secondError);
        Assert.Equal(0, secondErrorType);
        Assert.NotNull(equations);
        Assert.Equal(2, equations.Count);
    }

    [AvaloniaFact(Timeout = 5_000)]
    public void LegacyEquationImportProducesStructuralMathMlAndThePiGlyph()
    {
        using var grapher = new Grapher();
        var equation = new Equation
        {
            Expression = "sqrt(sin(x)*pi)"
        };

        grapher.Equations.Add(equation);

        MathParseResult parsed = MathInterchange.Parse(equation.MathMl, MathTextFormat.MathMl);
        Assert.Empty(parsed.Diagnostics);
        Assert.IsType<MathRadical>(Assert.Single(parsed.Document.Root.Children));
        Assert.Contains(
            "π",
            MathInterchange.Serialize(parsed.Document, MathTextFormat.UnicodeMath),
            StringComparison.Ordinal);
    }

    [AvaloniaFact(Timeout = 5_000)]
    public void AddingAnExistingMathMlEquationDoesNotRewriteItsDocument()
    {
        using var grapher = new Grapher();
        string mathMl = MathMl("sqrt(sin(x)×π)");
        var equation = new Equation
        {
            MathMl = mathMl
        };

        grapher.Equations.Add(equation);

        Assert.Equal(mathMl, equation.MathMl);
    }

    [AvaloniaFact(Timeout = 5_000)]
    public void FunctionAnalysisPublishesNativeMathDocuments()
    {
        using var grapher = new Grapher();
        var equation = new Equation
        {
            MathMl = MathMl("sin(x)")
        };

        KeyGraphFeaturesInfo analysis = Assert.IsType<KeyGraphFeaturesInfo>(
            grapher.AnalyzeEquation(equation));

        Assert.NotEmpty(analysis.Documents.Domain.Root.Children);
        Assert.NotEmpty(analysis.Documents.Range.Root.Children);
        Assert.NotEmpty(analysis.Documents.Zeros.Root.Children);
        Assert.NotEmpty(analysis.Documents.Periodicity.Root.Children);
        Assert.Empty(equation.Expression);
        Assert.Contains(
            analysis.Documents.Zeros.Root.Children,
            static node => node is MathScript);
        Assert.Contains(
            "π",
            MathInterchange.Serialize(analysis.Documents.Zeros, MathTextFormat.UnicodeMath),
            StringComparison.Ordinal);
    }

    private static string MathMl(string unicodeMath)
    {
        MathDocument document = MathInterchange.Parse(
            unicodeMath,
            MathTextFormat.UnicodeMath).Document;
        return MathInterchange.Serialize(document, MathTextFormat.MathMl);
    }
}
