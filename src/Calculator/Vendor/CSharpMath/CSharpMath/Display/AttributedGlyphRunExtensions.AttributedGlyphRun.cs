using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace CSharpMath.Display;

public static class AttributedGlyphRunExtensions
{
    public static bool AttributesMatch<TFont, TGlyph>(this AttributedGlyphRun<TFont, TGlyph>? run1, AttributedGlyphRun<TFont, TGlyph>? run2)
        where TFont : FrontEnd.IFont<TGlyph> => run1 != null && run2 != null && EqualityComparer<TFont>.Default.Equals(run1.Font, run2.Font);
}
