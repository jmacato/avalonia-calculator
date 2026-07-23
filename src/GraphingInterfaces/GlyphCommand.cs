namespace Graphing;

public sealed record GlyphCommand(string Text, GraphPoint Origin, string FontFamily, float FontSize, GraphPaint Paint, GraphTextAlignment Alignment = GraphTextAlignment.Start, GraphFontStyle FontStyle = GraphFontStyle.Normal) : GraphFrameCommand(GraphCommandKind.Glyph);
