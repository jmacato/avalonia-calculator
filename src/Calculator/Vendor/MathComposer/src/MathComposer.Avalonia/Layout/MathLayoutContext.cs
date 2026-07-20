using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Avalonia;
using MathComposer.Avalonia.OpenType;
using MathComposer.Core;

namespace MathComposer.Avalonia.Layout;

internal sealed class MathLayoutContext
{
    private readonly OpenTypeMathFont _font;
    private readonly HashSet<int> _missingScalars = [];

    public MathLayoutContext(OpenTypeMathFont font)
    {
        _font = font;
    }

    public List<MathDiagnostic> Diagnostics { get; } = [];

    public MathLayoutBox LayoutRow(
        MathRow row,
        ImmutableArray<int> path,
        double size,
        int scriptLevel)
    {
        if (row.Children.IsEmpty)
        {
            double ascent = size * 0.65;
            double descent = size * 0.2;
            double width = size * 0.55;
            var empty = new MathLayoutBox(width, ascent, descent);
            empty.Commands.Add(new MathPlaceholderDrawCommand(
                new Rect(size * 0.08, -ascent * 0.72, width - size * 0.16, ascent * 0.82)));
            empty.CaretStops.Add(Caret(path, 0, 0, ascent, descent));
            return empty;
        }

        var result = new MathLayoutBox(0, size * 0.75, size * 0.25);
        double x = 0;
        result.CaretStops.Add(Caret(path, 0, x, result.Ascent, result.Descent));
        MathNode? previous = null;
        for (int index = 0; index < row.Children.Length; index++)
        {
            MathNode child = row.Children[index];
            double spacing = previous is null ? 0 : AtomSpacing(previous, child, size);
            x += spacing;
            MathLayoutBox childBox = LayoutNode(child, path.Add(index), size, scriptLevel);
            result.Add(childBox, x, 0);
            result.Ascent = Math.Max(result.Ascent, childBox.Ascent);
            result.Descent = Math.Max(result.Descent, childBox.Descent);
            x += childBox.Width;
            result.CaretStops.Add(Caret(path, index + 1, x, result.Ascent, result.Descent));
            previous = child;
        }

        result.Width = x;
        result.UpdateRowCaretHeights(path);
        return result;
    }

    private MathLayoutBox LayoutNode(
        MathNode node,
        ImmutableArray<int> path,
        double size,
        int scriptLevel) => node switch
        {
            MathRow row => LayoutRow(row, path, size, scriptLevel),
            MathText text => LayoutText(text.Text, path, text.AtomClass, size, includeCaretStops: true),
            MathFraction fraction => LayoutFraction(fraction, path, size, scriptLevel),
            MathRadical radical => LayoutRadical(radical, path, size, scriptLevel),
            MathFunction function => LayoutFunction(function, path, size, scriptLevel),
            MathScript script => LayoutScript(script, path, size, scriptLevel),
            MathUnderOver underOver => LayoutUnderOver(underOver, path, size, scriptLevel),
            MathAccent accent => LayoutAccent(accent, path, size, scriptLevel),
            MathDelimiter delimiter => LayoutDelimiter(delimiter, path, size, scriptLevel),
            MathTable table => LayoutTable(table, path, size, scriptLevel),
            MathSpacing spacing => new MathLayoutBox(SpacingWidth(spacing.Width, size), size * 0.7, size * 0.2),
            MathError error => LayoutError(error, size),
            _ => throw new ArgumentException($"Unsupported node {node.GetType().Name}.", nameof(node))
        };

    private MathLayoutBox LayoutText(
        string text,
        ImmutableArray<int> path,
        MathAtomClass atomClass,
        double size,
        bool includeCaretStops)
    {
        double ascent = _font.ScaleDesignUnits(_font.Ascender, size);
        double descent = _font.ScaleDesignUnits(-_font.Descender, size);
        var box = new MathLayoutBox(0, Math.Max(size * 0.5, ascent), Math.Max(size * 0.15, descent));
        box.Commands.Add(new MathTextDrawCommand(text, new Point(0, 0), size));
        double x = 0;
        if (includeCaretStops)
        {
            box.CaretStops.Add(Caret(path, 0, 0, box.Ascent, box.Descent));
        }

        int utf16Offset = 0;
        foreach (Rune rune in text.EnumerateRunes())
        {
            ushort glyph = _font.GetGlyphId(rune);
            double advance;
            if (glyph == 0)
            {
                advance = size * (atomClass == MathAtomClass.OrdinaryText ? 0.55 : 0.6);
                ReportMissing(rune);
            }
            else
            {
                advance = _font.ScaleDesignUnits(_font.GetGlyphMetrics(glyph).AdvanceWidth, size);
            }

            x += advance;
            utf16Offset += rune.Utf16SequenceLength;
            if (includeCaretStops)
            {
                box.CaretStops.Add(Caret(path, utf16Offset, x, box.Ascent, box.Descent));
            }
        }

        box.Width = Math.Max(size * 0.08, x);
        return box;
    }

    private MathLayoutBox LayoutFraction(
        MathFraction fraction,
        ImmutableArray<int> path,
        double size,
        int scriptLevel)
    {
        double childSize = ScriptSize(size, scriptLevel + 1);
        MathLayoutBox numerator = LayoutRow(fraction.Numerator, path.Add(0), childSize, scriptLevel + 1);
        MathLayoutBox denominator = LayoutRow(fraction.Denominator, path.Add(1), childSize, scriptLevel + 1);
        double rule = PositiveConstant(OpenTypeMathConstant.FractionRuleThickness, size, size / 18);
        double numeratorGap = PositiveConstant(
            OpenTypeMathConstant.FractionNumeratorGapMin,
            size,
            rule);
        double denominatorGap = PositiveConstant(
            OpenTypeMathConstant.FractionDenominatorGapMin,
            size,
            rule);
        double axis = Scale(OpenTypeMathConstant.AxisHeight, size);
        double padding = size * 0.12;
        double width = Math.Max(numerator.Width, denominator.Width) + padding * 2;
        double ruleCenter = -axis;
        double numeratorShift = PositiveConstant(
            OpenTypeMathConstant.FractionNumeratorShiftUp,
            size,
            size * 0.35);
        double denominatorShift = PositiveConstant(
            OpenTypeMathConstant.FractionDenominatorShiftDown,
            size,
            size * 0.35);
        double numeratorBaseline = Math.Min(
            -numeratorShift,
            ruleCenter - rule / 2 - numeratorGap - numerator.Descent);
        double denominatorBaseline = Math.Max(
            denominatorShift,
            ruleCenter + rule / 2 + denominatorGap + denominator.Ascent);
        double ascent = Math.Max(-numeratorBaseline + numerator.Ascent, axis + rule / 2);
        double descent = Math.Max(denominatorBaseline + denominator.Descent, -axis + rule / 2);
        var box = new MathLayoutBox(width, ascent, descent);
        box.Add(numerator, (width - numerator.Width) / 2, numeratorBaseline);
        box.Add(denominator, (width - denominator.Width) / 2, denominatorBaseline);
        box.Commands.Add(new MathRuleDrawCommand(
            new Rect(padding * 0.45, ruleCenter - rule / 2, width - padding * 0.9, rule)));
        return box;
    }

    private MathLayoutBox LayoutRadical(
        MathRadical radical,
        ImmutableArray<int> path,
        double size,
        int scriptLevel)
    {
        MathLayoutBox radicand = LayoutRow(radical.Radicand, path.Add(0), size, scriptLevel);
        double gap = PositiveConstant(OpenTypeMathConstant.RadicalVerticalGap, size, size / 18);
        double rule = PositiveConstant(OpenTypeMathConstant.RadicalRuleThickness, size, size / 18);
        double extraAscender = Math.Max(0, Scale(OpenTypeMathConstant.RadicalExtraAscender, size));
        double target = radicand.Ascent + radicand.Descent + gap + rule;
        MathLayoutBox radicalGlyph = StretchGlyph(new Rune('√'), target, size, vertical: true);
        double degreeReserve = radical.Degree is null ? 0 : size * 0.28;
        double x = degreeReserve + radicalGlyph.Width;
        double ascent = Math.Max(radicalGlyph.Ascent, radicand.Ascent + gap + rule + extraAscender);
        double descent = Math.Max(radicalGlyph.Descent, radicand.Descent);
        var box = new MathLayoutBox(x + radicand.Width + size * 0.05, ascent, descent);
        box.Add(radicalGlyph, degreeReserve, 0);
        box.Add(radicand, x, 0);
        box.Commands.Add(new MathRuleDrawCommand(
            new Rect(x, -radicand.Ascent - gap - rule, radicand.Width + size * 0.05, rule)));
        if (radical.Degree is not null)
        {
            double degreeSize = ScriptSize(size, scriptLevel + 2);
            MathLayoutBox degree = LayoutRow(radical.Degree, path.Add(1), degreeSize, scriptLevel + 2);
            double raise = target * _font.RadicalDegreeBottomRaisePercent / 100d;
            double kernBefore = Scale(OpenTypeMathConstant.RadicalKernBeforeDegree, size);
            double kernAfter = Scale(OpenTypeMathConstant.RadicalKernAfterDegree, size);
            double degreeX = Math.Max(0, degreeReserve - degree.Width + kernBefore + kernAfter);
            box.Add(degree, degreeX, -raise);
            box.Ascent = Math.Max(box.Ascent, raise + degree.Ascent);
        }

        return box;
    }

    private MathLayoutBox LayoutFunction(
        MathFunction function,
        ImmutableArray<int> path,
        double size,
        int scriptLevel)
    {
        MathLayoutBox name = LayoutText(
            function.Name,
            [],
            MathAtomClass.OrdinaryText,
            size,
            includeCaretStops: false);
        double x = 0;
        var box = new MathLayoutBox(0, name.Ascent, name.Descent);
        box.Add(name, x, 0);
        x += name.Width;
        int argumentIndex = 0;
        if (function is { Name: "log", Arguments.Length: 2 })
        {
            double baseSize = ScriptSize(size, scriptLevel + 1);
            MathLayoutBox logarithmBase = LayoutRow(
                function.Arguments[0],
                path.Add(0),
                baseSize,
                scriptLevel + 1);
            double shift = PositiveConstant(OpenTypeMathConstant.SubscriptShiftDown, size, size * 0.2);
            box.Add(logarithmBase, x, shift);
            x += logarithmBase.Width;
            box.Descent = Math.Max(box.Descent, shift + logarithmBase.Descent);
            argumentIndex = 1;
        }

        MathLayoutBox opening = LayoutText("(", [], MathAtomClass.Operator, size, includeCaretStops: false);
        MathLayoutBox argument = LayoutRow(
            function.Arguments[argumentIndex],
            path.Add(argumentIndex),
            size,
            scriptLevel);
        MathLayoutBox closing = LayoutText(")", [], MathAtomClass.Operator, size, includeCaretStops: false);
        x += size * 0.1;
        box.Add(opening, x, 0);
        x += opening.Width;
        box.Add(argument, x, 0);
        x += argument.Width;
        box.Add(closing, x, 0);
        x += closing.Width;
        box.Width = x;
        box.Ascent = Math.Max(box.Ascent, Math.Max(argument.Ascent, opening.Ascent));
        box.Descent = Math.Max(box.Descent, Math.Max(argument.Descent, opening.Descent));
        return box;
    }

    private MathLayoutBox LayoutScript(
        MathScript script,
        ImmutableArray<int> path,
        double size,
        int scriptLevel)
    {
        MathLayoutBox @base = LayoutNode(script.Base, path.Add(0), size, scriptLevel);
        int nextPathIndex = 1;
        double scriptSize = ScriptSize(size, scriptLevel + 1);
        MathLayoutBox? subscript = script.Subscript is null
            ? null
            : LayoutRow(script.Subscript, path.Add(nextPathIndex++), scriptSize, scriptLevel + 1);
        MathLayoutBox? superscript = script.Superscript is null
            ? null
            : LayoutRow(script.Superscript, path.Add(nextPathIndex), scriptSize, scriptLevel + 1);
        double superscriptShift = PositiveConstant(
            OpenTypeMathConstant.SuperscriptShiftUp,
            size,
            size * 0.4);
        double subscriptShift = PositiveConstant(
            OpenTypeMathConstant.SubscriptShiftDown,
            size,
            size * 0.2);
        if (superscript is not null)
        {
            double bottomMinimum = PositiveConstant(
                OpenTypeMathConstant.SuperscriptBottomMin,
                size,
                size * 0.08);
            double baselineDropMaximum = Math.Max(
                0,
                Scale(OpenTypeMathConstant.SuperscriptBaselineDropMax, size));
            superscriptShift = Math.Max(
                superscriptShift,
                Math.Max(
                    superscript.Descent + bottomMinimum,
                    @base.Ascent - baselineDropMaximum));
        }

        if (subscript is not null)
        {
            double baselineDropMinimum = Math.Max(
                0,
                Scale(OpenTypeMathConstant.SubscriptBaselineDropMin, size));
            subscriptShift = Math.Max(
                subscriptShift,
                @base.Descent + baselineDropMinimum);
            double topMaximum = Scale(OpenTypeMathConstant.SubscriptTopMax, size);
            if (double.IsFinite(topMaximum))
            {
                subscriptShift = Math.Min(
                    subscriptShift,
                    Math.Max(0, subscript.Ascent + topMaximum));
            }
        }
        if (subscript is not null && superscript is not null)
        {
            double minimumGap = PositiveConstant(
                OpenTypeMathConstant.SubSuperscriptGapMin,
                size,
                size * 0.15);
            double actualGap = superscriptShift - superscript.Descent +
                               subscriptShift - subscript.Ascent;
            if (actualGap < minimumGap)
            {
                subscriptShift += minimumGap - actualGap;
            }
        }

        double scriptOffset = ScriptHorizontalOffset(script.Base, size, superscriptShift, subscriptShift);
        double scriptWidth = Math.Max(subscript?.Width ?? 0, superscript?.Width ?? 0);
        double gapAfter = Scale(OpenTypeMathConstant.SpaceAfterScript, size);
        var box = new MathLayoutBox(
            @base.Width + scriptOffset + scriptWidth + Math.Max(0, gapAfter),
            @base.Ascent,
            @base.Descent);
        box.Add(@base, 0, 0);
        if (superscript is not null)
        {
            box.Add(superscript, @base.Width + scriptOffset, -superscriptShift);
            box.Ascent = Math.Max(box.Ascent, superscriptShift + superscript.Ascent);
        }

        if (subscript is not null)
        {
            box.Add(subscript, @base.Width + scriptOffset, subscriptShift);
            box.Descent = Math.Max(box.Descent, subscriptShift + subscript.Descent);
        }

        return box;
    }

    private MathLayoutBox LayoutUnderOver(
        MathUnderOver underOver,
        ImmutableArray<int> path,
        double size,
        int scriptLevel)
    {
        MathLayoutBox @base = underOver.Kind == MathUnderOverKind.NaryLimits
            ? LayoutNaryBase(underOver.Base, path.Add(0), size, scriptLevel)
            : LayoutRow(underOver.Base, path.Add(0), size, scriptLevel);
        MathLayoutBox decorated = underOver.Kind switch
        {
            MathUnderOverKind.Overbar => AddBar(@base, size, over: true),
            MathUnderOverKind.Underbar => AddBar(@base, size, over: false),
            MathUnderOverKind.Overbrace => AddBrace(@base, size, over: true),
            MathUnderOverKind.Underbrace => AddBrace(@base, size, over: false),
            _ => @base
        };
        int nextPathIndex = 1;
        double annotationSize = ScriptSize(size, scriptLevel + 1);
        MathLayoutBox? below = underOver.Below is null
            ? null
            : LayoutRow(underOver.Below, path.Add(nextPathIndex++), annotationSize, scriptLevel + 1);
        MathLayoutBox? above = underOver.Above is null
            ? null
            : LayoutRow(underOver.Above, path.Add(nextPathIndex), annotationSize, scriptLevel + 1);
        double upperGap = PositiveConstant(OpenTypeMathConstant.UpperLimitGapMin, size, size * 0.12);
        double lowerGap = PositiveConstant(OpenTypeMathConstant.LowerLimitGapMin, size, size * 0.12);
        double width = Math.Max(decorated.Width, Math.Max(below?.Width ?? 0, above?.Width ?? 0));
        var box = new MathLayoutBox(width, decorated.Ascent, decorated.Descent);
        box.Add(decorated, (width - decorated.Width) / 2, 0);
        if (above is not null)
        {
            double rise = Math.Max(
                decorated.Ascent + upperGap + above.Descent,
                PositiveConstant(
                    OpenTypeMathConstant.UpperLimitBaselineRiseMin,
                    size,
                    decorated.Ascent + upperGap));
            double baseline = -rise;
            box.Add(above, (width - above.Width) / 2, baseline);
            box.Ascent = Math.Max(box.Ascent, -baseline + above.Ascent);
        }

        if (below is not null)
        {
            double baseline = Math.Max(
                decorated.Descent + lowerGap + below.Ascent,
                PositiveConstant(
                    OpenTypeMathConstant.LowerLimitBaselineDropMin,
                    size,
                    decorated.Descent + lowerGap));
            box.Add(below, (width - below.Width) / 2, baseline);
            box.Descent = Math.Max(box.Descent, baseline + below.Descent);
        }

        return box;
    }

    private MathLayoutBox LayoutAccent(
        MathAccent accent,
        ImmutableArray<int> path,
        double size,
        int scriptLevel)
    {
        MathLayoutBox @base = LayoutRow(accent.Base, path.Add(0), size, scriptLevel);
        Rune marker = new(AccentScalar(accent.Kind));
        MathLayoutBox glyph = StretchGlyph(marker, @base.Width, size, vertical: false);
        double gap = size * 0.06;
        double baseAttachment = TopAccentAttachment(accent.Base, size, @base.Width / 2);
        ushort accentGlyph = _font.GetGlyphId(marker);
        double glyphAttachment = accentGlyph == 0
            ? glyph.Width / 2
            : _font.ScaleDesignUnits(_font.GetTopAccentAttachment(accentGlyph), size);
        double glyphX = baseAttachment - glyphAttachment;
        double shift = Math.Max(0, -glyphX);
        glyphX += shift;
        double baseX = shift;
        double width = Math.Max(baseX + @base.Width, glyphX + glyph.Width);
        var box = new MathLayoutBox(width, @base.Ascent, @base.Descent);
        box.Add(@base, baseX, 0);
        if (accent.Placement == MathAccentPlacement.Over)
        {
            double baseline = -@base.Ascent - gap - glyph.Descent;
            box.Add(glyph, glyphX, baseline);
            box.Ascent = Math.Max(box.Ascent, -baseline + glyph.Ascent);
        }
        else
        {
            double baseline = @base.Descent + gap + glyph.Ascent;
            box.Add(glyph, glyphX, baseline);
            box.Descent = Math.Max(box.Descent, baseline + glyph.Descent);
        }

        return box;
    }

    private MathLayoutBox LayoutDelimiter(
        MathDelimiter delimiter,
        ImmutableArray<int> path,
        double size,
        int scriptLevel)
    {
        MathLayoutBox body = LayoutRow(delimiter.Body, path.Add(0), size, scriptLevel);
        double target = body.Ascent + body.Descent + size * 0.12;
        if (delimiter.Scalable)
        {
            target = Math.Max(
                target,
                _font.ScaleDesignUnits(_font.DelimitedSubFormulaMinimumHeight, size));
        }
        MathLayoutBox? opening = delimiter.Opening is null
            ? null
            : StretchGlyph(FirstRune(delimiter.Opening), target, size, delimiter.Scalable);
        MathLayoutBox? closing = delimiter.Closing is null
            ? null
            : StretchGlyph(FirstRune(delimiter.Closing), target, size, delimiter.Scalable);
        double x = 0;
        double ascent = body.Ascent;
        double descent = body.Descent;
        var box = new MathLayoutBox(0, ascent, descent);
        if (opening is not null)
        {
            box.Add(opening, x, 0);
            x += opening.Width + size * 0.06;
            box.Ascent = Math.Max(box.Ascent, opening.Ascent);
            box.Descent = Math.Max(box.Descent, opening.Descent);
        }

        box.Add(body, x, 0);
        x += body.Width;
        if (closing is not null)
        {
            x += size * 0.06;
            box.Add(closing, x, 0);
            x += closing.Width;
            box.Ascent = Math.Max(box.Ascent, closing.Ascent);
            box.Descent = Math.Max(box.Descent, closing.Descent);
        }

        box.Width = x;
        return box;
    }

    private MathLayoutBox LayoutTable(
        MathTable table,
        ImmutableArray<int> path,
        double size,
        int scriptLevel)
    {
        int rowCount = table.Rows.Length;
        int columnCount = table.Rows[0].Length;
        var cells = new MathLayoutBox[rowCount][];
        for (int row = 0; row < rowCount; row++)
        {
            cells[row] = new MathLayoutBox[columnCount];
        }
        var widths = new double[columnCount];
        var ascents = new double[rowCount];
        var descents = new double[rowCount];
        for (int row = 0; row < rowCount; row++)
        {
            for (int column = 0; column < columnCount; column++)
            {
                int pathIndex = row * columnCount + column;
                MathLayoutBox cell = LayoutRow(table.Rows[row][column], path.Add(pathIndex), size, scriptLevel);
                cells[row][column] = cell;
                widths[column] = Math.Max(widths[column], cell.Width);
                ascents[row] = Math.Max(ascents[row], cell.Ascent);
                descents[row] = Math.Max(descents[row], cell.Descent);
            }
        }

        double columnGap = table.Kind == MathTableKind.Aligned ? size * 0.8 : size * 0.55;
        double rowGap = size * 0.32;
        double totalWidth = widths.Sum() + columnGap * (columnCount - 1);
        double totalHeight = ascents.Sum() + descents.Sum() + rowGap * (rowCount - 1);
        double top = -totalHeight / 2;
        var box = new MathLayoutBox(totalWidth, totalHeight / 2, totalHeight / 2);
        double y = top;
        for (int row = 0; row < rowCount; row++)
        {
            double baseline = y + ascents[row];
            double x = 0;
            for (int column = 0; column < columnCount; column++)
            {
                MathLayoutBox cell = cells[row][column];
                double cellX = table.Kind == MathTableKind.Aligned
                    ? column % 2 == 0 ? x + widths[column] - cell.Width : x
                    : x + (widths[column] - cell.Width) / 2;
                box.Add(cell, cellX, baseline);
                x += widths[column] + columnGap;
            }

            y += ascents[row] + descents[row] + rowGap;
        }

        return box;
    }

    private MathLayoutBox LayoutError(MathError error, double size)
    {
        string visible = error.RawFragment.Length == 0 ? "error" : error.RawFragment;
        MathLayoutBox text = LayoutText(
            visible,
            [],
            MathAtomClass.OrdinaryText,
            size,
            includeCaretStops: false);
        text.Commands.Clear();
        text.Commands.Add(new MathTextDrawCommand(visible, new Point(0, 0), size, IsError: true));
        double line = Math.Max(1, size / 18);
        text.Commands.Add(new MathRuleDrawCommand(
            new Rect(0, text.Descent * 0.35, text.Width, line),
            IsError: true));
        return text;
    }

    private MathLayoutBox AddBar(MathLayoutBox @base, double size, bool over)
    {
        double gap = PositiveConstant(
            over ? OpenTypeMathConstant.OverbarVerticalGap : OpenTypeMathConstant.UnderbarVerticalGap,
            size,
            size * 0.12);
        double rule = PositiveConstant(
            over ? OpenTypeMathConstant.OverbarRuleThickness : OpenTypeMathConstant.UnderbarRuleThickness,
            size,
            size / 18);
        var box = new MathLayoutBox(@base.Width, @base.Ascent, @base.Descent);
        box.Add(@base, 0, 0);
        if (over)
        {
            double y = -@base.Ascent - gap - rule;
            box.Commands.Add(new MathRuleDrawCommand(new Rect(0, y, @base.Width, rule)));
            box.Ascent = Math.Max(box.Ascent, -y);
        }
        else
        {
            double y = @base.Descent + gap;
            box.Commands.Add(new MathRuleDrawCommand(new Rect(0, y, @base.Width, rule)));
            box.Descent = Math.Max(box.Descent, y + rule);
        }

        return box;
    }

    private MathLayoutBox AddBrace(MathLayoutBox @base, double size, bool over)
    {
        Rune brace = new(over ? 0x23DE : 0x23DF);
        MathLayoutBox glyph = StretchGlyph(brace, @base.Width, size, vertical: false);
        var box = new MathLayoutBox(Math.Max(@base.Width, glyph.Width), @base.Ascent, @base.Descent);
        box.Add(@base, (box.Width - @base.Width) / 2, 0);
        if (over)
        {
            double baseline = -@base.Ascent - size * 0.06 - glyph.Descent;
            box.Add(glyph, (box.Width - glyph.Width) / 2, baseline);
            box.Ascent = Math.Max(box.Ascent, -baseline + glyph.Ascent);
        }
        else
        {
            double baseline = @base.Descent + size * 0.06 + glyph.Ascent;
            box.Add(glyph, (box.Width - glyph.Width) / 2, baseline);
            box.Descent = Math.Max(box.Descent, baseline + glyph.Descent);
        }

        return box;
    }

    private MathLayoutBox StretchGlyph(Rune rune, double target, double size, bool vertical)
    {
        ushort glyph = _font.GetGlyphId(rune);
        if (glyph == 0)
        {
            ReportMissing(rune);
            return LayoutText(rune.ToString(), [], MathAtomClass.Operator, size, includeCaretStops: false);
        }

        OpenTypeMathGlyphDirection direction = vertical
            ? OpenTypeMathGlyphDirection.Vertical
            : OpenTypeMathGlyphDirection.Horizontal;
        OpenTypeMathGlyphConstruction? construction = _font.GetGlyphConstruction(glyph, direction);
        double targetDesignUnits = target * _font.UnitsPerEm / size;
        ushort selectedGlyph = glyph;
        double selectedMeasurement = vertical
            ? _font.Ascender - _font.Descender
            : _font.GetGlyphMetrics(glyph).AdvanceWidth;
        bool variantMeetsTarget = selectedMeasurement >= targetDesignUnits;
        if (construction is not null)
        {
            foreach (OpenTypeMathGlyphVariant variant in construction.Variants)
            {
                selectedGlyph = variant.GlyphId;
                selectedMeasurement = variant.AdvanceMeasurement;
                if (variant.AdvanceMeasurement >= targetDesignUnits)
                {
                    variantMeetsTarget = true;
                    break;
                }
            }

            if (!variantMeetsTarget && construction.Assembly is not null)
            {
                return LayoutGlyphAssembly(
                    construction.Assembly,
                    targetDesignUnits,
                    size,
                    vertical);
            }
        }

        double advance = _font.ScaleDesignUnits(
            _font.GetGlyphMetrics(selectedGlyph).AdvanceWidth,
            size);
        double measurement = _font.ScaleDesignUnits(selectedMeasurement, size);
        double ascent;
        double descent;
        if (vertical)
        {
            double axis = Scale(OpenTypeMathConstant.AxisHeight, size);
            ascent = measurement / 2 + axis;
            descent = Math.Max(size * 0.1, measurement - ascent);
        }
        else
        {
            ascent = _font.ScaleDesignUnits(_font.Ascender, size);
            descent = _font.ScaleDesignUnits(-_font.Descender, size);
            advance = measurement;
        }

        var box = new MathLayoutBox(Math.Max(size * 0.08, advance), ascent, descent);
        box.Commands.Add(new MathGlyphDrawCommand(selectedGlyph, new Point(0, 0), size));
        return box;
    }

    private MathLayoutBox LayoutNaryBase(
        MathRow row,
        ImmutableArray<int> path,
        double size,
        int scriptLevel)
    {
        if (row.Children is not [MathText text] ||
            Rune.DecodeFromUtf16(text.Text, out Rune rune, out int consumed) != OperationStatus.Done ||
            consumed != text.Text.Length)
        {
            return LayoutRow(row, path, size, scriptLevel);
        }

        double target = _font.ScaleDesignUnits(_font.DisplayOperatorMinimumHeight, size);
        MathLayoutBox glyph = StretchGlyph(rune, target, size, vertical: true);
        glyph.CaretStops.Add(Caret(path, 0, 0, glyph.Ascent, glyph.Descent));
        glyph.CaretStops.Add(Caret(path.Add(0), 0, 0, glyph.Ascent, glyph.Descent));
        glyph.CaretStops.Add(Caret(
            path.Add(0),
            text.Text.Length,
            glyph.Width,
            glyph.Ascent,
            glyph.Descent));
        glyph.CaretStops.Add(Caret(path, 1, glyph.Width, glyph.Ascent, glyph.Descent));
        return glyph;
    }

    private double ScriptHorizontalOffset(
        MathNode @base,
        double size,
        double superscriptShift,
        double subscriptShift)
    {
        if (@base is not MathText text ||
            Rune.DecodeFromUtf16(text.Text, out Rune rune, out int consumed) != OperationStatus.Done ||
            consumed != text.Text.Length)
        {
            return 0;
        }

        ushort glyph = _font.GetGlyphId(rune);
        if (glyph == 0)
        {
            return 0;
        }

        short superscriptHeight = (short)Math.Clamp(
            Math.Round(superscriptShift * _font.UnitsPerEm / size),
            short.MinValue,
            short.MaxValue);
        short subscriptHeight = (short)Math.Clamp(
            Math.Round(-subscriptShift * _font.UnitsPerEm / size),
            short.MinValue,
            short.MaxValue);
        int designUnits = _font.GetItalicsCorrection(glyph);
        designUnits += Math.Max(
            _font.GetMathKern(glyph, OpenTypeMathKernCorner.TopRight, superscriptHeight),
            _font.GetMathKern(glyph, OpenTypeMathKernCorner.BottomRight, subscriptHeight));
        return Math.Max(0, _font.ScaleDesignUnits(designUnits, size));
    }

    private double TopAccentAttachment(MathRow row, double size, double fallback)
    {
        if (row.Children is not [MathText text] ||
            Rune.DecodeFromUtf16(text.Text, out Rune rune, out int consumed) != OperationStatus.Done ||
            consumed != text.Text.Length)
        {
            return fallback;
        }

        ushort glyph = _font.GetGlyphId(rune);
        return glyph == 0
            ? fallback
            : _font.ScaleDesignUnits(_font.GetTopAccentAttachment(glyph), size);
    }

    private MathLayoutBox LayoutGlyphAssembly(
        OpenTypeMathGlyphAssembly assembly,
        double targetDesignUnits,
        double size,
        bool vertical)
    {
        ImmutableArray<OpenTypeMathGlyphPart> parts = ExpandAssemblyParts(
            assembly.Parts,
            targetDesignUnits);
        if (parts.IsDefaultOrEmpty)
        {
            throw new InvalidDataException("A MATH glyph assembly contains no parts.");
        }

        var minimumOverlaps = new double[Math.Max(0, parts.Length - 1)];
        var maximumOverlaps = new double[minimumOverlaps.Length];
        double totalAdvance = 0;
        for (int index = 0; index < parts.Length; index++)
        {
            totalAdvance += parts[index].FullAdvance;
            if (index + 1 < parts.Length)
            {
                double maximum = Math.Min(
                    parts[index].EndConnectorLength,
                    parts[index + 1].StartConnectorLength);
                maximumOverlaps[index] = maximum;
                minimumOverlaps[index] = Math.Min(_font.MinimumConnectorOverlap, maximum);
            }
        }

        double minimumOverlapTotal = minimumOverlaps.Sum();
        double maximumOverlapTotal = maximumOverlaps.Sum();
        double desiredOverlap = Math.Clamp(
            totalAdvance - targetDesignUnits,
            minimumOverlapTotal,
            maximumOverlapTotal);
        var overlaps = (double[])minimumOverlaps.Clone();
        double remaining = desiredOverlap - minimumOverlapTotal;
        for (int index = 0; index < overlaps.Length && remaining > 0; index++)
        {
            double addition = Math.Min(remaining, maximumOverlaps[index] - overlaps[index]);
            overlaps[index] += addition;
            remaining -= addition;
        }

        double measurementDesignUnits = totalAdvance - overlaps.Sum();
        double measurement = ScaleArbitrary(measurementDesignUnits, size);
        if (vertical)
        {
            return LayoutVerticalAssembly(parts, overlaps, measurement, size);
        }

        return LayoutHorizontalAssembly(parts, overlaps, measurement, size);
    }

    private MathLayoutBox LayoutVerticalAssembly(
        ImmutableArray<OpenTypeMathGlyphPart> parts,
        double[] overlaps,
        double measurement,
        double size)
    {
        double axis = Scale(OpenTypeMathConstant.AxisHeight, size);
        double ascent = measurement / 2 + axis;
        double descent = Math.Max(size * 0.1, measurement - ascent);
        double width = 0;
        foreach (OpenTypeMathGlyphPart part in parts)
        {
            width = Math.Max(
                width,
                _font.ScaleDesignUnits(_font.GetGlyphMetrics(part.GlyphId).AdvanceWidth, size));
        }

        var box = new MathLayoutBox(Math.Max(size * 0.08, width), ascent, descent);
        double cursor = measurement;
        for (int index = 0; index < parts.Length; index++)
        {
            OpenTypeMathGlyphPart part = parts[index];
            double fullAdvance = ScaleArbitrary(part.FullAdvance, size);
            double partTop = cursor - fullAdvance;
            OpenTypeGlyphBounds bounds = _font.GetGlyphBounds(part.GlyphId);
            double glyphHeight = ScaleArbitrary(bounds.YMax - bounds.YMin, size);
            double extra = Math.Max(0, fullAdvance - glyphHeight) / 2;
            double baseline = partTop - ascent + extra + ScaleArbitrary(bounds.YMax, size);
            double glyphWidth = _font.ScaleDesignUnits(
                _font.GetGlyphMetrics(part.GlyphId).AdvanceWidth,
                size);
            box.Commands.Add(new MathGlyphDrawCommand(
                part.GlyphId,
                new Point((box.Width - glyphWidth) / 2, baseline),
                size));
            if (index < overlaps.Length)
            {
                cursor = partTop + ScaleArbitrary(overlaps[index], size);
            }
        }

        return box;
    }

    private MathLayoutBox LayoutHorizontalAssembly(
        ImmutableArray<OpenTypeMathGlyphPart> parts,
        double[] overlaps,
        double measurement,
        double size)
    {
        double ascent = _font.ScaleDesignUnits(_font.Ascender, size);
        double descent = _font.ScaleDesignUnits(-_font.Descender, size);
        var box = new MathLayoutBox(Math.Max(size * 0.08, measurement), ascent, descent);
        double cursor = 0;
        for (int index = 0; index < parts.Length; index++)
        {
            OpenTypeMathGlyphPart part = parts[index];
            double fullAdvance = ScaleArbitrary(part.FullAdvance, size);
            OpenTypeGlyphBounds bounds = _font.GetGlyphBounds(part.GlyphId);
            double inkWidth = ScaleArbitrary(bounds.XMax - bounds.XMin, size);
            double extra = Math.Max(0, fullAdvance - inkWidth) / 2;
            double origin = cursor + extra - ScaleArbitrary(bounds.XMin, size);
            box.Commands.Add(new MathGlyphDrawCommand(
                part.GlyphId,
                new Point(origin, 0),
                size));
            cursor += fullAdvance;
            if (index < overlaps.Length)
            {
                cursor -= ScaleArbitrary(overlaps[index], size);
            }
        }

        return box;
    }

    private ImmutableArray<OpenTypeMathGlyphPart> ExpandAssemblyParts(
        ImmutableArray<OpenTypeMathGlyphPart> source,
        double targetDesignUnits)
    {
        int extenderCount = source.Count(static part => part.IsExtender);
        if (extenderCount == 0)
        {
            return source;
        }

        double initialMaximum = AssemblyMaximumAdvance(source);
        double growth = 0;
        foreach (OpenTypeMathGlyphPart part in source)
        {
            if (part.IsExtender)
            {
                growth += Math.Max(1, part.FullAdvance - _font.MinimumConnectorOverlap);
            }
        }

        int additionalCopies = targetDesignUnits <= initialMaximum
            ? 0
            : (int)Math.Ceiling((targetDesignUnits - initialMaximum) / growth);
        additionalCopies = Math.Clamp(additionalCopies, 0, 2048 / extenderCount);
        var expanded = ImmutableArray.CreateBuilder<OpenTypeMathGlyphPart>(
            source.Length + additionalCopies * extenderCount);
        foreach (OpenTypeMathGlyphPart part in source)
        {
            int copies = part.IsExtender ? additionalCopies + 1 : 1;
            for (int copy = 0; copy < copies; copy++)
            {
                expanded.Add(part);
            }
        }

        return expanded.ToImmutable();
    }

    private double AssemblyMaximumAdvance(ImmutableArray<OpenTypeMathGlyphPart> parts)
    {
        double result = parts.Sum(static part => (double)part.FullAdvance);
        for (int index = 0; index + 1 < parts.Length; index++)
        {
            double maximum = Math.Min(
                parts[index].EndConnectorLength,
                parts[index + 1].StartConnectorLength);
            result -= Math.Min(_font.MinimumConnectorOverlap, maximum);
        }

        return result;
    }

    private double ScaleArbitrary(double designUnits, double size) =>
        designUnits * size / _font.UnitsPerEm;

    private double ScriptSize(double size, int level)
    {
        int percentage = level <= 1
            ? _font.ScriptPercentScaleDown
            : _font.ScriptScriptPercentScaleDown;
        return Math.Max(size * 0.45, size * percentage / 100d);
    }

    private double Scale(OpenTypeMathConstant constant, double size) =>
        _font.ScaleDesignUnits(_font.GetMathConstant(constant), size);

    private double PositiveConstant(
        OpenTypeMathConstant constant,
        double size,
        double fallback)
    {
        double value = Scale(constant, size);
        return value > 0 && double.IsFinite(value) ? value : fallback;
    }

    private static double AtomSpacing(MathNode left, MathNode right, double size)
    {
        MathAtomClass? leftClass = AtomClass(left);
        MathAtomClass? rightClass = AtomClass(right);
        if (leftClass == MathAtomClass.Relation || rightClass == MathAtomClass.Relation)
        {
            return size * 5 / 18;
        }

        if (leftClass == MathAtomClass.Operator || rightClass == MathAtomClass.Operator)
        {
            return size * 4 / 18;
        }

        if (leftClass == MathAtomClass.Punctuation)
        {
            return size * 3 / 18;
        }

        return 0;
    }

    private static MathAtomClass? AtomClass(MathNode node) => node switch
    {
        MathText text => text.AtomClass,
        MathFunction => MathAtomClass.Identifier,
        MathSpacing => null,
        _ => MathAtomClass.OrdinaryText
    };

    private static double SpacingWidth(MathSpacingWidth width, double size) => width switch
    {
        MathSpacingWidth.Thin => size * 3 / 18,
        MathSpacingWidth.Medium => size * 4 / 18,
        MathSpacingWidth.Em => size,
        _ => throw new ArgumentOutOfRangeException(nameof(width))
    };

    private void ReportMissing(Rune rune)
    {
        if (_missingScalars.Add(rune.Value))
        {
            Diagnostics.Add(new MathDiagnostic(
                "MC5001",
                MathDiagnosticSeverity.Warning,
                $"The math font does not contain U+{rune.Value:X4}; platform fallback is required.",
                MathTextFormat.UnicodeMath));
        }
    }

    private static Rune FirstRune(string text)
    {
        Rune.DecodeFromUtf16(text, out Rune rune, out _);
        return rune;
    }

    private static int AccentScalar(MathAccentKind kind) => kind switch
    {
        MathAccentKind.Acute => 0x0301,
        MathAccentKind.Grave => 0x0300,
        MathAccentKind.Hat => 0x0302,
        MathAccentKind.Check => 0x030C,
        MathAccentKind.Breve => 0x0306,
        MathAccentKind.Tilde => 0x0303,
        MathAccentKind.Bar => 0x0304,
        MathAccentKind.Dot => 0x0307,
        MathAccentKind.DoubleDot => 0x0308,
        MathAccentKind.TripleDot => 0x20DB,
        MathAccentKind.Vector => 0x20D7,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static MathCaretStop Caret(
        ImmutableArray<int> path,
        int offset,
        double x,
        double ascent,
        double descent) =>
        new(
            new MathPosition(path, offset),
            new Rect(x - 1.5, -ascent, 3, Math.Max(1, ascent + descent)));
}
