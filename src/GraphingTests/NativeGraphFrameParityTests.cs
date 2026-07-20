using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using Graphing;
using Graphing.Renderer;
using GraphingRaster.Skia;
using JsMath.Port;
using SkiaSharp;

namespace GraphingTests;

public sealed class NativeGraphFrameParityTests
{
    private const uint NativeWidth = 587;
    private const uint NativeHeight = 848;
    private static readonly Color NativeBlue = new(0, 99, 177);
    private static readonly Color NativeGrid = new(198, 198, 198);
    private static readonly string[] ExpectedGlyphTexts = ["-6", "-4", "-2", "0", "2", "4", "6", "-10", "-5", "5", "10", "x", "y"];
    [Fact]
    public void GridUsesFiveMinorIntervalsAndCapturedNativePaints()
    {
        NativeGraphFrameParityTestsRenderedGraph rendered = Render("x");
        StrokePathCommand[] grid = rendered.Frame.Commands.OfType<StrokePathCommand>().Where(command => SameRgb(command.Paint.Color, NativeGrid)).ToArray();
        Assert.Equal(66, grid.Length);
        Assert.Equal(14, grid.Count(command => command.Paint.Color.A == byte.MaxValue));
        Assert.Equal(52, grid.Count(command => command.Paint.Color.A == 127));
        Assert.All(grid, command =>
        {
            Assert.Equal(1, command.Paint.StrokeWidth);
            Assert.Equal(LineStyle.Solid, command.Paint.LineStyle);
        });
        StrokePathCommand[] vertical = grid.Where(IsVertical).OrderBy(LineCoordinate).ToArray();
        StrokePathCommand[] horizontal = grid.Where(IsHorizontal).OrderBy(LineCoordinate).ToArray();
        Assert.Equal(41, vertical.Length);
        Assert.Equal(25, horizontal.Length);
        AssertFiveSubdivisions(vertical);
        AssertFiveSubdivisions(horizontal);
    }

    [Fact]
    public void AxisAliasesCoverTickLabelsAndEquationGeometryStaysOnTop()
    {
        NativeGraphFrameParityTestsRenderedGraph rendered = Render("x");
        GraphFrameCommand[] commands = rendered.Frame.Commands.ToArray();
        GlyphCommand[] glyphs = commands.OfType<GlyphCommand>().ToArray();
        GlyphBackgroundCommand[] backgrounds = commands.OfType<GlyphBackgroundCommand>().ToArray();
        StrokePathCommand[] axes = commands.OfType<StrokePathCommand>().Where(command => command.Paint.Color == new Color(0, 0, 0)).ToArray();
        Assert.Equal(6, axes.Length);
        Assert.All(axes, command => Assert.Equal(1, command.Paint.StrokeWidth));
        Assert.Equal(13, glyphs.Length);
        Assert.Equal(13, backgrounds.Length);
        Assert.All(glyphs, glyph =>
        {
            Assert.Equal("Segoe UI", glyph.FontFamily);
            Assert.Equal(12, glyph.FontSize);
            Assert.Equal(GraphFontStyle.Italic, glyph.FontStyle);
            Assert.Equal(new Color(0, 0, 0), glyph.Paint.Color);
        });
        Assert.All(backgrounds, background =>
        {
            Assert.Contains(background.Glyph, glyphs);
            Assert.Equal(new Color(255, 255, 255), background.Paint.Color);
        });
        Assert.Equal(ExpectedGlyphTexts, glyphs.Select(glyph => glyph.Text));
        GlyphCommand xAlias = Assert.Single(glyphs, glyph => glyph.Text == "x");
        GlyphCommand yAlias = Assert.Single(glyphs, glyph => glyph.Text == "y");
        GlyphCommand zero = Assert.Single(glyphs, glyph => glyph.Text == "0");
        AssertPoint(xAlias.Origin, 580, 429);
        AssertPoint(yAlias.Origin, 282.5, -0.98046875);
        AssertPoint(zero.Origin, 289.5, 426);
        Assert.Equal(GraphTextAlignment.Center, xAlias.Alignment);
        Assert.Equal(GraphTextAlignment.End, yAlias.Alignment);
        Assert.Equal(GraphTextAlignment.End, zero.Alignment);
        int lastEquation = Array.FindLastIndex(commands, IsBlueStroke);
        Assert.True(lastEquation >= 0);
        Assert.All(backgrounds, background => Assert.Same(background.Glyph, commands[Array.IndexOf(commands, background) + 1]));
        Assert.All(backgrounds, background => Assert.True(Array.IndexOf(commands, background) < lastEquation));
        Assert.All(glyphs, glyph => Assert.True(Array.IndexOf(commands, glyph) < lastEquation));
        Assert.True(Array.IndexOf(commands, xAlias) > Array.IndexOf(commands, zero));
        Assert.True(Array.IndexOf(commands, yAlias) > Array.IndexOf(commands, zero));
        Assert.Empty(commands.OfType<MarkerCommand>());
    }

    [Fact]
    public void StrictAndInclusiveInequalitiesUseNativeHatchAndBoundarySemantics()
    {
        NativeGraphFrameParityTestsRenderedGraph strict = Render("y>x", selected: true);
        NativeGraphFrameParityTestsRenderedGraph inclusive = Render("y>=x");
        HatchGridCommand strictHatch = Assert.Single(strict.Frame.Commands.OfType<HatchGridCommand>());
        HatchGridCommand inclusiveHatch = Assert.Single(inclusive.Frame.Commands.OfType<HatchGridCommand>());
        Assert.Equal(1673, strictHatch.Occupancy.Sum(word => BitOperations.PopCount(word)));
        Assert.Equal(1673, inclusiveHatch.Occupancy.Sum(word => BitOperations.PopCount(word)));
        Assert.True(strictHatch.Occupancy.SequenceEqual(inclusiveHatch.Occupancy));
        AssertNativeHatch(strictHatch);
        AssertNativeHatch(inclusiveHatch);
        Assert.Empty(strict.Frame.Commands.OfType<MarkerCommand>());
        Assert.Empty(inclusive.Frame.Commands.OfType<MarkerCommand>());
        Assert.Empty(strict.Frame.Commands.OfType<FillPathCommand>());
        Assert.Empty(inclusive.Frame.Commands.OfType<FillPathCommand>());
        StrokePathCommand[] strictBoundary = BlueStrokes(strict.Frame);
        StrokePathCommand[] inclusiveBoundary = BlueStrokes(inclusive.Frame);
        Assert.Equal(2, strictBoundary.Length);
        Assert.Equal(2, inclusiveBoundary.Length);
        Assert.All(strictBoundary, command =>
        {
            Assert.Equal(3, command.Paint.StrokeWidth);
            Assert.Equal(LineStyle.Dash, command.Paint.LineStyle);
        });
        Assert.All(inclusiveBoundary, command =>
        {
            Assert.Equal(2, command.Paint.StrokeWidth);
            Assert.Equal(LineStyle.Solid, command.Paint.LineStyle);
        });
        Assert.Equal(strictBoundary[0].Path.Points, strictBoundary[1].Path.Points);
        Assert.Equal(inclusiveBoundary[0].Path.Points, inclusiveBoundary[1].Path.Points);
        AssertPoint(strictBoundary[0].Path.Points[0], 0, 717.5);
        AssertPoint(strictBoundary[0].Path.Points[^1], 587, 130.5);
    }

    [Fact]
    public void ZoomedOutExplicitInequalityReusesTheStandaloneCurveBoundary()
    {
        NativeGraphFrameParityTestsRenderedGraph standalone = Render("sin(x)*log(x)", xMin: -128, xMax: 128, yMin: -8, yMax: 8);
        NativeGraphFrameParityTestsRenderedGraph inequality = Render("sin(x)*log(x)<y", xMin: -128, xMax: 128, yMin: -8, yMax: 8);
        AssertInequalityReusesBoundary(standalone.Frame, inequality.Frame);
    }

    [Fact]
    public void ImplicitInequalityReusesTheEqualityContour()
    {
        NativeGraphFrameParityTestsRenderedGraph equality = Render("x^2+y^2=16");
        NativeGraphFrameParityTestsRenderedGraph inequality = Render("x^2+y^2<16");
        AssertInequalityReusesBoundary(equality.Frame, inequality.Frame);
    }

    [Fact]
    public void PngExportUsesPhysicalDpiAndExactAliasedCrossPixels()
    {
        var blue = new GraphPaint(NativeBlue, 1, LineStyle.Solid, AntiAlias: false);
        var frame = new GraphFrame(20, 20, 192, 192, 1, new Color(255, 255, 255), [new HatchGridCommand([2UL], 2, 20, 20, 1, blue)]);
        byte[] png = SkiaGraphFrameRasterizer.EncodePng(frame).ToArray();
        Assert.Equal(40, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)));
        Assert.Equal(40, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
        using SKBitmap bitmap = SKBitmap.Decode(png) ?? throw new InvalidOperationException("Skia did not decode its PNG output.");
        var actualBlue = new HashSet<(int X, int Y)>();
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) == new SKColor(0, 99, 177))
                {
                    actualBlue.Add((x, y));
                }
            }
        }

        var expectedBlue = new HashSet<(int X, int Y)>();
        for (int y = 19; y <= 20; y++)
        {
            for (int x = 17; x <= 22; x++)
            {
                expectedBlue.Add((x, y));
            }
        }

        for (int y = 17; y <= 22; y++)
        {
            for (int x = 19; x <= 20; x++)
            {
                expectedBlue.Add((x, y));
            }
        }

        Assert.True(expectedBlue.SetEquals(actualBlue));
    }

    [Fact]
    public void DashedDiagonalUsesCapturedEuclideanCadence()
    {
        var frame = new GraphFrame(100, 100, 192, 192, 1, new Color(255, 255, 255), [new StrokePathCommand(new GraphPath([new GraphPoint(0, 90), new GraphPoint(90, 0)]), new GraphPaint(NativeBlue, 3, LineStyle.Dash))]);
        using SKBitmap bitmap = SKBitmap.Decode(SkiaGraphFrameRasterizer.EncodePng(frame).ToArray()) ?? throw new InvalidOperationException("Skia did not decode its PNG output.");
        List<(int Start, int Length)> runs = BlueRuns(bitmap, 0, 180, 180, 0);
        Assert.True(runs.Count >= 8);
        double meanPeriod = runs.Zip(runs.Skip(1), (left, right) => right.Start - left.Start).Average();
        double meanLength = runs.Skip(1).SkipLast(1).Average(run => run.Length);
        Assert.InRange(meanPeriod, 23, 25);
        Assert.InRange(meanLength, 10, 14);
    }

    [Fact]
    public void MarchingSquaresStitchesBothEndsIntoOneConnectedBoundary()
    {
        var viewport = new SamplingViewport(new AxisRange(-7.7, 7.7), new AxisRange(-11.1236797274276, 11.1236797274276), NativeWidth, NativeHeight);
        InequalityMesh mesh = MarchingSquares.Build((x, y) => y - x, value => value > 0, viewport, columns: 73, rows: 106, cancellationToken: TestContext.Current.CancellationToken);
        ImmutableArray<GraphPoint> contour = Assert.Single(mesh.Contours);
        Assert.True(contour.Length > 100);
        Assert.Equal(-7.7, contour[0].X, 10);
        Assert.Equal(-7.7, contour[0].Y, 10);
        Assert.Equal(7.7, contour[^1].X, 10);
        Assert.Equal(7.7, contour[^1].Y, 10);
    }

    [Fact]
    public void ContourOnlyMeshingPreservesBoundariesWithoutRetainingFillCells()
    {
        var viewport = new SamplingViewport(new AxisRange(-7.7, 7.7), new AxisRange(-11.1, 11.1), NativeWidth, NativeHeight);
        ImplicitEvaluator evaluator = (x, y) => Math.Sin(12 * x) + Math.Sin(9 * y);
        InequalityMesh full = MarchingSquares.Build(evaluator, value => value > 0, viewport, columns: 73, rows: 106, maximumVertices: 65_536, cancellationToken: TestContext.Current.CancellationToken);
        InequalityMesh contoursOnly = MarchingSquares.BuildContours(evaluator, value => value > 0, viewport, columns: 73, rows: 106, maximumVertices: 65_536, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Empty(contoursOnly.FilledPolygons);
        Assert.Equal(full.EvaluationCount, contoursOnly.EvaluationCount);
        Assert.Equal(full.Contours.Length, contoursOnly.Contours.Length);
        for (int index = 0; index < full.Contours.Length; index++)
        {
            Assert.Equal(full.Contours[index], contoursOnly.Contours[index]);
        }
    }

    [Fact]
    public void SpatialStitcherPreservesReverseSegmentPriorityAtAmbiguousJunctions()
    {
        // This low-resolution surface has two endpoints within the stitch
        // tolerance. The original reverse scan chooses the later segment and
        // tests its A endpoint first; the spatial index must preserve that
        // deterministic ordering rather than depend on hash-bucket traversal.
        double[] c = [2.3310765802539306, 3.322560388279409, -1.68154023107213, 1.3519500816948478, 1.7044826698044702, 1.7484990906661837, -2.197549860085151, -2.9083885768933166, 1.405922478719578, -1.1320270733591293];
        var viewport = new SamplingViewport(new AxisRange(-3, 3), new AxisRange(-2, 2), 320, 240);
        InequalityMesh mesh = MarchingSquares.BuildContours((x, y) => c[0] + (c[1] * x) + (c[2] * y) + (c[3] * x * y) + (c[4] * x * x) + (c[5] * y * y) + (c[6] * Math.Sin(c[7] * x)) + (c[8] * Math.Cos(c[9] * y)), value => value > 0, viewport, columns: 3, rows: 15, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal([21, 3], mesh.Contours.Select(contour => contour.Length));
        ImmutableArray<GraphPoint> shortContour = mesh.Contours[1];
        AssertPoint(shortContour[0], -0.9966470121371801, 0.6666666666666665);
        AssertPoint(shortContour[^1], -0.996585889746418, 0.6662114519661889);
    }

    private static NativeGraphFrameParityTestsRenderedGraph Render(string formula, bool selected = false, double xMin = -7.7, double xMax = 7.7, double yMin = -11.1236797274276, double yMax = 11.1236797274276)
    {
        IMathSolver solver = MathSolver.CreateMathSolver();
        solver.ParsingOptions().SetFormatType(FormatType.Linear);
        IExpression expression = solver.ParseInput(formula, out int errorCode, out int errorType) ?? throw new InvalidOperationException($"Parse failed: {errorCode}/{errorType}");
        IGraph graph = solver.CreateGrapher();
        IReadOnlyList<IEquation> equations = graph.TryInitialize(expression) ?? throw new InvalidOperationException($"Initialize failed: {graph.GetInitializationError()}");
        IEquation equation = Assert.Single(equations);
        if (selected)
        {
            Assert.True(equation.TrySelectEquation());
        }

        IGraphRenderer renderer = graph.GetRenderer();
        Assert.Equal(GraphStatus.Ok, renderer.SetGraphSize(NativeWidth, NativeHeight));
        Assert.Equal(GraphStatus.Ok, renderer.SetDpi(192, 192));
        Assert.Equal(GraphStatus.Ok, renderer.SetDisplayRanges(xMin, xMax, yMin, yMax));
        Assert.Equal(GraphStatus.Ok, renderer.PrepareGraph());
        GraphFrame frame = renderer.CurrentFrame ?? throw new InvalidOperationException("Renderer did not publish a frame.");
        return new NativeGraphFrameParityTestsRenderedGraph(graph, equation, renderer, frame);
    }

    private static StrokePathCommand[] BlueStrokes(GraphFrame frame) => frame.Commands.OfType<StrokePathCommand>().Where(IsBlueStroke).ToArray();
    private static void AssertInequalityReusesBoundary(GraphFrame standalone, GraphFrame inequality)
    {
        StrokePathCommand[] standaloneBoundary = BlueStrokes(standalone);
        StrokePathCommand[] inequalityBoundary = BlueStrokes(inequality);
        Assert.NotEmpty(standaloneBoundary);
        Assert.Equal(standaloneBoundary.Length * 2, inequalityBoundary.Length);
        for (int index = 0; index < standaloneBoundary.Length; index++)
        {
            GraphPath expected = standaloneBoundary[index].Path;
            GraphPath first = inequalityBoundary[index * 2].Path;
            GraphPath second = inequalityBoundary[(index * 2) + 1].Path;
            Assert.Equal(expected.IsClosed, first.IsClosed);
            Assert.Equal(expected.IsClosed, second.IsClosed);
            Assert.Equal(expected.Points, first.Points);
            Assert.Equal(first.Points, second.Points);
        }
    }

    private static bool IsBlueStroke(GraphFrameCommand command) => command is StrokePathCommand stroke && IsBlueStroke(stroke);
    private static bool IsBlueStroke(StrokePathCommand command) => command.Paint.Color == NativeBlue;
    private static bool SameRgb(Color left, Color right) => left.R == right.R && left.G == right.G && left.B == right.B;
    private static bool IsVertical(StrokePathCommand command) => command.Path.Points.Length == 2 && Math.Abs(command.Path.Points[0].X - command.Path.Points[1].X) < 1e-10;
    private static bool IsHorizontal(StrokePathCommand command) => command.Path.Points.Length == 2 && Math.Abs(command.Path.Points[0].Y - command.Path.Points[1].Y) < 1e-10;
    private static double LineCoordinate(StrokePathCommand command) => IsVertical(command) ? command.Path.Points[0].X : command.Path.Points[0].Y;
    private static void AssertFiveSubdivisions(StrokePathCommand[] lines)
    {
        StrokePathCommand[] majors = lines.Where(line => line.Paint.Color.A == byte.MaxValue).ToArray();
        for (int index = 1; index < majors.Length; index++)
        {
            double left = LineCoordinate(majors[index - 1]);
            double right = LineCoordinate(majors[index]);
            Assert.Equal(4, lines.Count(line =>
            {
                double coordinate = LineCoordinate(line);
                return line.Paint.Color.A == 127 && coordinate > left && coordinate < right;
            }));
        }
    }

    private static void AssertNativeHatch(HatchGridCommand hatch)
    {
        Assert.Equal(58, hatch.LatticeIntervals);
        Assert.Equal(NativeWidth, hatch.Width);
        Assert.Equal(NativeHeight, hatch.Height);
        Assert.Equal(1, hatch.Radius);
        Assert.Equal(NativeBlue, hatch.Paint.Color);
        Assert.Equal(1, hatch.Paint.StrokeWidth);
        Assert.Equal(LineStyle.Solid, hatch.Paint.LineStyle);
        Assert.False(hatch.Paint.AntiAlias);
    }

    private static void AssertPoint(GraphPoint actual, double expectedX, double expectedY)
    {
        Assert.Equal(expectedX, actual.X, 9);
        Assert.Equal(expectedY, actual.Y, 9);
    }

    private static List<(int Start, int Length)> BlueRuns(SKBitmap bitmap, double startX, double startY, double endX, double endY)
    {
        double dx = endX - startX;
        double dy = endY - startY;
        double length = Math.Sqrt((dx * dx) + (dy * dy));
        var samples = new bool[(int)Math.Floor(length) + 1];
        for (int distance = 0; distance < samples.Length; distance++)
        {
            double amount = distance / length;
            int x = (int)Math.Round(startX + (dx * amount));
            int y = (int)Math.Round(startY + (dy * amount));
            SKColor color = bitmap.GetPixel(x, y);
            samples[distance] = color.Red < 30 && color.Green is > 75 and < 125 && color.Blue > 145;
        }

        var runs = new List<(int Start, int Length)>();
        int runStart = -1;
        for (int index = 0; index <= samples.Length; index++)
        {
            bool value = index < samples.Length && samples[index];
            if (value && runStart < 0)
            {
                runStart = index;
            }
            else if (!value && runStart >= 0)
            {
                runs.Add((runStart, index - runStart));
                runStart = -1;
            }
        }

        return runs;
    }
}
