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
    private readonly GlyphTypeface _glyphTypeface;

    /// <summary>Initializes the renderer and resolves the required embedded math font.</summary>
    public MathRenderer()
    {
        MathFontFamily = new FontFamily(
            "avares://MathComposer.Avalonia/Assets/Fonts#XCharter Math");
        _typeface = new Typeface(MathFontFamily);
        if (!FontManager.Current.TryGetGlyphTypeface(_typeface, out GlyphTypeface? glyphTypeface))
        {
            throw new InvalidOperationException(
                "The embedded XCharter Math font could not be initialized.");
        }

        _glyphTypeface = glyphTypeface;
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
                    DrawGlyph(context, glyph, origin, foreground, renderScaling);
                    break;
                case MathRuleDrawCommand rule:
                    context.FillRectangle(
                        rule.IsError ? Brushes.Crimson : foreground,
                        Snap(Translate(rule.Bounds, origin), renderScaling));
                    break;
                case MathPlaceholderDrawCommand placeholder:
                    DrawPlaceholder(context, placeholder, origin, foreground, renderScaling);
                    break;
            }
        }

        MathCaretStop? caret = FindCaret(layout, selection.Active);
        if (caret is not null && preeditText.Length > 0)
        {
            DrawPreedit(context, caret.Value, preeditText, origin, foreground, renderScaling);
        }

        if (focused && selection.IsCollapsed && caret is not null)
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

    private void DrawGlyph(
        DrawingContext context,
        MathGlyphDrawCommand command,
        Point origin,
        IBrush foreground,
        double renderScaling)
    {
        Point baseline = origin + command.BaselineOrigin;
        baseline = new Point(
            Snap(baseline.X, renderScaling),
            Snap(baseline.Y, renderScaling));
        using var glyphRun = new GlyphRun(
            _glyphTypeface,
            command.FontSize,
            ReadOnlyMemory<char>.Empty,
            new[] { command.GlyphId },
            baseline);
        context.DrawGlyphRun(foreground, glyphRun);
    }

    private static void DrawPlaceholder(
        DrawingContext context,
        MathPlaceholderDrawCommand command,
        Point origin,
        IBrush foreground,
        double renderScaling)
    {
        Rect bounds = Snap(Translate(command.Bounds, origin), renderScaling);
        var fill = new SolidColorBrush(Color.FromArgb(28, 43, 124, 224));
        context.DrawRectangle(fill, new Pen(foreground, 1), bounds);
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

    private static Rect Translate(Rect rectangle, Point origin) =>
        new(rectangle.X + origin.X, rectangle.Y + origin.Y, rectangle.Width, rectangle.Height);

    private static Rect Snap(Rect rectangle, double scaling)
    {
        double left = Snap(rectangle.Left, scaling);
        double top = Snap(rectangle.Top, scaling);
        double right = Snap(rectangle.Right, scaling);
        double bottom = Snap(rectangle.Bottom, scaling);
        return new Rect(left, top, Math.Max(1 / scaling, right - left), Math.Max(1 / scaling, bottom - top));
    }

    private static double Snap(double value, double scaling) => Math.Round(value * scaling) / scaling;
}
