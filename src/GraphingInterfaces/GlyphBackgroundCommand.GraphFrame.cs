using System.Collections.Immutable;
using Graphing.Renderer;

namespace Graphing;
/// <summary>
/// Clears the measured layout rectangle for a positioned glyph without drawing
/// the glyph itself. Keeping this separate lets equation geometry pass over the
/// cleared axes/grid while the glyph is replayed last, matching Calculator's
/// authored draw order.
/// </summary>
public sealed record GlyphBackgroundCommand(GlyphCommand Glyph, GraphPaint Paint) : GraphFrameCommand(GraphCommandKind.GlyphBackground);
