using CSharpMath.Atom;
using System.Drawing;

namespace CSharpMath.Display
{
    using FrontEnd;

    public interface IDisplay<TFont, TGlyph>
        where TFont : IFont<TGlyph>
    {
        void Draw(IGraphicsContext<TFont, TGlyph> context);
        /// <summary>By convention, Ascent and Descent should be positive
        /// numbers for the typical case where your font is partly above
        /// and partly below the baseline. This may differ from the
        /// convention of a particular OS, i.e. iOS.</summary>
        float Ascent { get; }

        float Descent { get; }

        float Width { get; }

        Range Range { get; }

        /// <summary>Position of the display, relative to its parent.</summary>
        PointF Position { get; set; }

        Color? TextColor { get; set; }

        void SetTextColorRecursive(Color? textColor);
        Color? BackColor { get; set; }

        bool HasScript { get; set; }
    }
}
