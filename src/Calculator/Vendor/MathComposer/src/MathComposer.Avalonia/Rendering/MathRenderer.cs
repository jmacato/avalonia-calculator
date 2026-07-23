using System.Globalization;
using Avalonia;
using Avalonia.Media;
using MathComposer.Avalonia.Layout;
using MathComposer.Core;

namespace MathComposer.Avalonia.Rendering;

/// <summary>Paints a layout result with the embedded XCharter Math font.</summary>
public sealed class MathRenderer
{
    private readonly Typeface _typeface;

    /// <summary>Initializes the renderer and resolves the required embedded math font.</summary>
    public MathRenderer()
    {
        MathFontFamily = new FontFamily(
            "avares://MathComposer.Avalonia/Assets/Fonts#XCharter Math");
        _typeface = new Typeface(MathFontFamily);
    }

    /// <summary>Gets the required embedded font family.</summary>
    public FontFamily MathFontFamily { get; }

    /// <summary>Paints selection, formula commands, IME preedit, placeholders, and caret.</summary>
    public void Render(
        DrawingContext context,
        MathLayoutResult layout,
        MathDocument document,
        MathSelection selection,
        Point origin,
        IBrush foreground,
        bool focused,
        bool caretVisible,
        MathSelection? inputRegion,
        string preeditText,
        double renderScaling = 1)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(foreground);
        ArgumentNullException.ThrowIfNull(preeditText);
        if (!double.IsFinite(renderScaling) || renderScaling <= 0)
        {
            renderScaling = 1;
        }

        GlyphTypeface glyphTypeface = ResolveGlyphTypeface();
        var selectionBrush = new SolidColorBrush(Color.FromArgb(72, 43, 124, 224));
        foreach (Rect rectangle in layout.GetSelectionRectangles(document, selection))
        {
            context.FillRectangle(selectionBrush, Translate(rectangle, origin));
        }

        foreach (MathDrawCommand command in layout.Commands)
        {
            switch (command)
            {
                case MathTextDrawCommand text:
                    DrawText(context, text, origin, foreground);
                    break;
                case MathGlyphDrawCommand glyph:
                    DrawGlyph(
                        context,
                        glyph,
                        origin,
                        foreground,
                        glyphTypeface,
                        renderScaling);
                    break;
                case MathRuleDrawCommand rule:
                    context.FillRectangle(
                        rule.IsError ? Brushes.Crimson : foreground,
                        Snap(Translate(rule.Bounds, origin), renderScaling));
                    break;
                case MathPlaceholderDrawCommand placeholder:
                    break;
            }
        }

        MathCaretStop? caret = FindCaret(layout, selection.Active);
        if (focused && caret is not null)
        {
            if (inputRegion is not null)
            {
                DrawInputRegion(
                    context,
                    layout,
                    document,
                    inputRegion.Value,
                    selection.Active,
                    caret.Value,
                    origin,
                    foreground,
                    renderScaling);
            }
            else
            {
                DrawActivePlaceholder(
                    context,
                    layout,
                    selection.Active,
                    origin,
                    foreground,
                    renderScaling);
            }
        }

        if (caret is not null && preeditText.Length > 0)
        {
            DrawPreedit(context, caret.Value, preeditText, origin, foreground, renderScaling);
        }

        if (focused && caretVisible && selection.IsCollapsed && caret is not null)
        {
            Rect bounds = Translate(caret.Value.Bounds, origin);
            double x = Snap(bounds.Center.X, renderScaling);
            context.DrawLine(
                new Pen(foreground, Math.Max(1 / renderScaling, 1)),
                new Point(x, Snap(bounds.Top, renderScaling)),
                new Point(x, Snap(bounds.Bottom, renderScaling)));
        }
    }

    private void DrawText(
        DrawingContext context,
        MathTextDrawCommand command,
        Point origin,
        IBrush foreground)
    {
        IBrush brush = command.IsError ? Brushes.Crimson : foreground;
        var formatted = new FormattedText(
            command.Text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            _typeface,
            command.FontSize,
            brush);
        Point baseline = origin + command.BaselineOrigin;
        context.DrawText(formatted, new Point(baseline.X, baseline.Y - formatted.Baseline));
    }

    private static void DrawGlyph(
        DrawingContext context,
        MathGlyphDrawCommand command,
        Point origin,
        IBrush foreground,
        GlyphTypeface glyphTypeface,
        double renderScaling)
    {
        Point baseline = origin + command.BaselineOrigin;
        baseline = new Point(
            Snap(baseline.X, renderScaling),
            Snap(baseline.Y, renderScaling));
        using var glyphRun = new GlyphRun(
            glyphTypeface,
            command.FontSize,
            ReadOnlyMemory<char>.Empty,
            new[] { command.GlyphId },
            baseline);
        context.DrawGlyphRun(foreground, glyphRun);
    }

    private GlyphTypeface ResolveGlyphTypeface()
    {
        if (!FontManager.Current.TryGetGlyphTypeface(
                _typeface,
                out GlyphTypeface? glyphTypeface))
        {
            throw new InvalidOperationException(
                "The embedded XCharter Math font could not be initialized.");
        }

        return glyphTypeface;
    }

    private static void DrawInputRegion(
        DrawingContext context,
        MathLayoutResult layout,
        MathDocument document,
        MathSelection inputRegion,
        MathPosition caretPosition,
        MathCaretStop caret,
        Point origin,
        IBrush foreground,
        double renderScaling)
    {
        var fill = new SolidColorBrush(Color.FromArgb(28, 43, 124, 224));
        var pen = new Pen(foreground, 1, DashStyle.Dash);
        if (!inputRegion.IsCollapsed)
        {
            foreach (Rect rectangle in layout.GetSelectionRectangles(document, inputRegion))
            {
                Rect bounds = Snap(Translate(rectangle, origin), renderScaling);
                context.DrawRectangle(fill, pen, bounds);
            }

            return;
        }

        foreach (MathDrawCommand drawCommand in layout.Commands)
        {
            if (drawCommand is MathPlaceholderDrawCommand placeholder &&
                placeholder.Position == caretPosition)
            {
                Rect bounds = Snap(Translate(placeholder.Bounds, origin), renderScaling);
                context.DrawRectangle(fill, pen, bounds);
                return;
            }
        }

        Rect caretBounds = Translate(caret.Bounds, origin);
        double width = Math.Max(6, caretBounds.Height * 0.45);
        var insertionBounds = new Rect(
            caretBounds.Center.X - width / 2,
            caretBounds.Top,
            width,
            caretBounds.Height);
        context.DrawRectangle(fill, pen, Snap(insertionBounds, renderScaling));
    }

    private static void DrawActivePlaceholder(
        DrawingContext context,
        MathLayoutResult layout,
        MathPosition caretPosition,
        Point origin,
        IBrush foreground,
        double renderScaling)
    {
        foreach (MathDrawCommand drawCommand in layout.Commands)
        {
            if (drawCommand is not MathPlaceholderDrawCommand placeholder ||
                placeholder.Position != caretPosition)
            {
                continue;
            }

            Rect bounds = Snap(Translate(placeholder.Bounds, origin), renderScaling);
            var fill = new SolidColorBrush(Color.FromArgb(28, 43, 124, 224));
            context.DrawRectangle(
                fill,
                new Pen(foreground, 1, DashStyle.Dash),
                bounds);
            return;
        }
    }

    private void DrawPreedit(
        DrawingContext context,
        MathCaretStop caret,
        string preeditText,
        Point origin,
        IBrush foreground,
        double renderScaling)
    {
        var formatted = new FormattedText(
            preeditText,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            _typeface,
            Math.Max(12, caret.Bounds.Height * 0.8),
            foreground);
        Rect caretBounds = Translate(caret.Bounds, origin);
        var topLeft = new Point(caretBounds.Center.X, caretBounds.Bottom - formatted.Baseline);
        context.DrawText(formatted, topLeft);
        double y = Snap(topLeft.Y + formatted.Height, renderScaling);
        context.DrawLine(
            new Pen(foreground, 1),
            new Point(topLeft.X, y),
            new Point(topLeft.X + formatted.Width, y));
    }

    private static MathCaretStop? FindCaret(MathLayoutResult layout, MathPosition position)
    {
        foreach (MathCaretStop stop in layout.CaretStops)
        {
            if (stop.Position == position)
            {
                return stop;
            }
        }

        return null;
    }

    private static Rect Translate(Rect rectangle, Point origin)
    {
        return new Rect(rectangle.X + origin.X, rectangle.Y + origin.Y, rectangle.Width, rectangle.Height);
    }

    private static Rect Snap(Rect rectangle, double scaling)
    {
        double left = Snap(rectangle.Left, scaling);
        double top = Snap(rectangle.Top, scaling);
        double right = Snap(rectangle.Right, scaling);
        double bottom = Snap(rectangle.Bottom, scaling);
        return new Rect(left, top, Math.Max(1 / scaling, right - left), Math.Max(1 / scaling, bottom - top));
    }

    private static double Snap(double value, double scaling)
    {
        return Math.Round(value * scaling) / scaling;
    }
}
