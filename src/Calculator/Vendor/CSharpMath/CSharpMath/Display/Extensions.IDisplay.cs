using CSharpMath.Atom;
using System.Drawing;

namespace CSharpMath
{
    using Display;
    using Display.FrontEnd;

    partial class Extensions
    {
        /// <summary>
        /// The display's bounds, in its own coordinate system.<br/>
        /// **Internal only! The Rectangle's position is at the bottom left: not what the outer world expects**
        /// </summary>
        public static RectangleF DisplayBounds<TFont, TGlyph>(this IDisplay<TFont, TGlyph> display)
            where TFont : IFont<TGlyph>
        {
            System.ArgumentNullException.ThrowIfNull(display);
            return new RectangleF(0, -display.Descent, display.Width, display.Ascent + display.Descent);
        }        /// <summary>Where the display is located, expressed in its parent's coordinate system.</summary>
        public static RectangleF Frame<TFont, TGlyph>(this IDisplay<TFont, TGlyph> display)
            where TFont : IFont<TGlyph>
        {
            System.ArgumentNullException.ThrowIfNull(display);
            return display.DisplayBounds().Plus(display.Position);
        }
        public static void DrawBackground<TFont, TGlyph>(this IDisplay<TFont, TGlyph> display, IGraphicsContext<TFont, TGlyph> context)
            where TFont : IFont<TGlyph>
        {
            System.ArgumentNullException.ThrowIfNull(context);
            System.ArgumentNullException.ThrowIfNull(display);
            if (display.BackColor is not { } color)
                return;
            context.SaveState();
            context.FillRect(display.Frame(), color);
            context.RestoreState();
        }
    }
}
