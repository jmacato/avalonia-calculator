using System;
using System.Drawing;

namespace CSharpMath.Rendering.FrontEnd
{
    public static class CanvasExtensions
    {
        public static void StrokeLineOutline(this ICanvas c, float x1, float y1, float x2, float y2, float lineThickness)
        {
            ArgumentNullException.ThrowIfNull(c);
            var dx = Math.Abs(x2 - x1);
            var dy = Math.Abs(y2 - y1);
            var length = (float)Math.Sqrt((double)dx * dx + (double)dy * dy);
            var halfThickness = lineThickness / 2;
            var px = dx / length * halfThickness;
            var py = dy / length * halfThickness;
            using var p = c.StartNewPath();
            p.MoveTo(x1 - py, y1 + px);
            p.LineTo(x1 + py, y1 - px);
            p.LineTo(x2 + py, y2 - px);
            p.LineTo(x2 - py, y2 + px);
            p.CloseContour();
        }
    }
}
