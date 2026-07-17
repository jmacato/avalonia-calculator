using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Graphing;
using Graphing.Renderer;
using AvaloniaColor = Avalonia.Media.Color;

namespace GraphControl;

internal readonly record struct AvaloniaGraphRenderCacheTextLayoutKey(string Text, string FontFamily, float FontSize, GraphFontStyle FontStyle, Graphing.Color Color);
