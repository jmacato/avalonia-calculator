using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;

public enum GraphCommandKind
{
    PushClip = 0,
    PopClip = 1,
    StrokePath = 2,
    FillPath = 3,
    Marker = 4,
    Glyph = 5,
    GlyphBackground = 6,
    PushCoordinateTransform = 7,
    PopCoordinateTransform = 8,
    CommandGroup = 9,
    HatchGrid = 10,
    StrokeLine = 11
}
