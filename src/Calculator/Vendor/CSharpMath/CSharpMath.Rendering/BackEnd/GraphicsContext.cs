using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CSharpMath.Display.FrontEnd;
using CSharpMath.Rendering.FrontEnd;
using CSharpMath.Structures;

namespace CSharpMath.Rendering.BackEnd;

public sealed class GraphicsContext : IGraphicsContext<Fonts, Glyph>
{
    public GraphicsContext(ICanvas canvas, (Color glyph, Color textRun)? glyphBoxColor)
    {
        Canvas = canvas;
        GlyphBoxColor = glyphBoxColor;
    }

    public (Color glyph, Color textRun)? GlyphBoxColor { get; set; }

    public ICanvas Canvas { get; set; }

    void IGraphicsContext<Fonts, Glyph>.SetTextPosition(PointF position) => Translate(position);

    public void DrawGlyphsAtPoints(
        IReadOnlyList<Glyph> glyphs,
        Fonts font,
        IEnumerable<PointF> points,
        Color? color)
    {
        foreach ((Glyph glyph, PointF point) in glyphs.Zip(points, ValueTuple.Create))
        {
            if (glyph.IsEmpty)
            {
                continue;
            }

            if (GlyphBoxColor is { } boxColor)
            {
                using var rentedArray = new RentedArray<Glyph>(glyph);
                RectangleF rectangle = GlyphBoundsProvider.Instance
                    .GetBoundingRectsForGlyphs(font, rentedArray.Result, 1)
                    .Single();
                Canvas.CurrentColor = boxColor.glyph;
                Canvas.StrokeRect(
                    point.X + rectangle.X,
                    point.Y + rectangle.Y,
                    rectangle.Width,
                    rectangle.Height);
            }

            Canvas.Save();
            Canvas.CurrentColor = color;
            Canvas.Translate(point.X, point.Y);
            Canvas.DrawGlyph(glyph.Typeface.Typeface, glyph.GlyphId, font.PixelSize);
            Canvas.Restore();
        }
    }

    public void DrawLine(
        float x1,
        float y1,
        float x2,
        float y2,
        float lineThickness,
        Color? color)
    {
        Canvas.CurrentColor = color;
        Canvas.DrawLine(x1, y1, x2, y2, lineThickness);
    }

    public void DrawGlyphRunWithOffset(
        Display.AttributedGlyphRun<Fonts, Glyph> run,
        PointF offset,
        Color? color)
    {
        if (GlyphBoxColor is { } boxColor)
        {
            float ascent = 0;
            float descent = 0;
            foreach ((Glyph glyph, _, _) in run.GlyphInfos)
            {
                Avalonia.Rect bounds = glyph.Typeface.GetInkBounds(glyph.GlyphId);
                float scale = run.Font.ScaleFor(glyph.Typeface);
                ascent = Math.Max(ascent, (float)-bounds.Top * scale);
                descent = Math.Min(descent, (float)-bounds.Bottom * scale);
            }

            float width = GlyphBoundsProvider.Instance.GetTypographicWidth(run.Font, run);
            Canvas.CurrentColor = boxColor.textRun;
            Canvas.StrokeRect(offset.X, offset.Y + descent, width, ascent - descent);
        }

        Canvas.Save();
        Canvas.Translate(offset.X, offset.Y);
        foreach ((Glyph glyph, float kernAfter, Color? foreground) in run.GlyphInfos)
        {
            if (glyph.IsEmpty)
            {
                continue;
            }

            Canvas.CurrentColor = foreground ?? color;
            Canvas.DrawGlyph(glyph.Typeface.Typeface, glyph.GlyphId, run.Font.PixelSize);
            Canvas.Translate(
                glyph.Typeface.GetAdvance(glyph.GlyphId) * run.Font.ScaleFor(glyph.Typeface) + kernAfter,
                0);
        }

        Canvas.Restore();
    }

    public void FillRect(RectangleF rectangle, Color color)
    {
        Canvas.CurrentColor = color;
        Canvas.FillRect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }

    public void RestoreState() => Canvas.Restore();

    public void SaveState() => Canvas.Save();

    public void Translate(PointF displacement) => Canvas.Translate(displacement.X, displacement.Y);
}
