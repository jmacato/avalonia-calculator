using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Avalonia;
using MathComposer.Avalonia.OpenType;
using MathComposer.Core;

namespace MathComposer.Avalonia.Layout;

/// <summary>Recursively lays out the immutable model using OpenType MATH metrics.</summary>
public sealed class MathLayoutEngine
{
    private readonly OpenTypeMathFont _font;

    /// <summary>Initializes a layout engine for one validated math font.</summary>
    public MathLayoutEngine(OpenTypeMathFont font)
    {
        _font = font ?? throw new ArgumentNullException(nameof(font));
    }

    /// <summary>Lays out a whole document without automatic line wrapping.</summary>
    public MathLayoutResult Layout(MathDocument document, double fontSize)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!double.IsFinite(fontSize) || fontSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        }

        var context = new MathLayoutContext(_font);
        MathLayoutBox root = context.LayoutRow(document.Root, [], fontSize, scriptLevel: 0);
        double height = Math.Max(1, root.Ascent + root.Descent);
        var commands = ImmutableArray.CreateBuilder<MathDrawCommand>(root.Commands.Count);
        foreach (MathDrawCommand command in root.Commands)
        {
            commands.Add(TranslateCommand(command, 0, root.Ascent));
        }

        var stops = ImmutableArray.CreateBuilder<MathCaretStop>(root.CaretStops.Count);
        foreach (MathCaretStop stop in root.CaretStops)
        {
            stops.Add(new MathCaretStop(
                stop.Position,
                stop.Bounds.Translate(new Vector(0, root.Ascent))));
        }

        return new MathLayoutResult(
            new Size(Math.Max(1, root.Width), height),
            root.Ascent,
            commands.ToImmutable(),
            stops.ToImmutable(),
            context.Diagnostics.ToImmutableArray());
    }

    internal static MathDrawCommand TranslateCommand(MathDrawCommand command, double x, double y) =>
        command switch
        {
            MathTextDrawCommand text => text with
            {
                BaselineOrigin = text.BaselineOrigin + new Vector(x, y)
            },
            MathGlyphDrawCommand glyph => glyph with
            {
                BaselineOrigin = glyph.BaselineOrigin + new Vector(x, y)
            },
            MathRuleDrawCommand rule => rule with
            {
                Bounds = rule.Bounds.Translate(new Vector(x, y))
            },
            MathPlaceholderDrawCommand placeholder => placeholder with
            {
                Bounds = placeholder.Bounds.Translate(new Vector(x, y))
            },
            _ => throw new ArgumentException("Unknown draw command.", nameof(command))
        };
}
