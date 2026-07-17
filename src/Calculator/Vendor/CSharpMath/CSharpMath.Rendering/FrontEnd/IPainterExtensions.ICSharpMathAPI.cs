using System.Drawing;
using GlyphTypeface = Avalonia.Media.GlyphTypeface;

namespace CSharpMath.Rendering.FrontEnd
{
    using Structures;

    public static class IPainterExtensions
    {
        public static PointF GetDisplayPosition(float displayWidth, float displayAscent, float displayDescent, float fontSize, float width, float height, TextAlignment alignment, Thickness padding, float offsetX, float offsetY)
        {
            // Canvas is inverted!
            if ((alignment & (TextAlignment.Top | TextAlignment.Bottom)) != 0)
            {
                alignment ^= TextAlignment.Top;
                alignment ^= TextAlignment.Bottom;
            }

            // Invert y-coordinate as canvas is inverted
            offsetY *= -1;
            var x = (alignment & TextAlignment.Left) != 0 ? padding.Left : (alignment & TextAlignment.Right) != 0 ? width - padding.Right - displayWidth : padding.Left + (width - padding.Left - padding.Right - displayWidth) / 2;
            float contentHeight = System.Math.Max(displayAscent + displayDescent, fontSize / 2);
            var y = (alignment & TextAlignment.Top) != 0 ? padding.Top + displayDescent : (alignment & TextAlignment.Bottom) != 0 ? height - padding.Bottom - displayAscent : (height - padding.Top - padding.Bottom - contentHeight) / 2 + padding.Top + displayDescent;
            return new PointF(x + offsetX, y + offsetY - height);
        }
    }
}
