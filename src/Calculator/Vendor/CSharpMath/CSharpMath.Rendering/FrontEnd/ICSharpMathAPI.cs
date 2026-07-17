using System.Drawing;
using GlyphTypeface = Avalonia.Media.GlyphTypeface;

namespace CSharpMath.Rendering.FrontEnd
{
    using Structures;

    public interface ICSharpMathAPI<TContent, TColor>
        where TContent : class
    {
        #region Non-display-recreating properties
        TColor HighlightColor { get; set; }

        TColor TextColor { get; set; }

        TColor ErrorColor { get; set; }

        ///<summary>Unit of measure: points; Defaults to <see cref = "FontSize"/>.</summary>
        float? ErrorFontSize { get; set; }

        bool DisplayErrorInline { get; set; }

        PaintStyle PaintStyle { get; set; }

        float Magnification { get; set; }

        string? ErrorMessage { get; }

        #endregion Non-display-recreating properties
        #region Display-recreating properties
        /// <summary>Unit of measure: points</summary>
        float FontSize { get; set; }

        System.Collections.Generic.IEnumerable<GlyphTypeface> LocalTypefaces { get; set; }

        Atom.LineStyle LineStyle { get; set; }

        TContent? Content { get; set; }

        string? LaTeX { get; set; }
        #endregion Display-recreating properties
    }
}
