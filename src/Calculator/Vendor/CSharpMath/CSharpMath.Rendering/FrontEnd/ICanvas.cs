using System;
using System.Drawing;

namespace CSharpMath.Rendering.FrontEnd
{
    public interface ICanvas
    {
        float Width { get; }

        float Height { get; }

        Color DefaultColor { get; set; }

        Color? CurrentColor { get; set; }

        PaintStyle CurrentStyle { get; set; }

        Path StartNewPath();
        void DrawGlyph(Avalonia.Media.GlyphTypeface typeface, ushort glyphId, float fontRenderingEmSize);
        void DrawLine(float x1, float y1, float x2, float y2, float lineThickness);
        void StrokeRect(float left, float top, float width, float height);
        void FillRect(float left, float top, float width, float height);
        void Save();
        void Translate(float dx, float dy);
        void Scale(float sx, float sy);
        void Restore();
    }
}
