using CSharpMath.Atom;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace CSharpMath.Display.Displays;

using FrontEnd;
public class TextLineDisplay<TFont, TGlyph>(
    IList<TextRunDisplay<TFont, TGlyph>> runs,
    IReadOnlyList<MathAtom> atoms,
    PointF position)
    : IDisplay<TFont, TGlyph>
    where TFont : IFont<TGlyph>
{
    public TextLineDisplay(
        AttributedString<TFont, TGlyph> text, Range range,
        TypesettingContext<TFont, TGlyph> context, IReadOnlyList<MathAtom> atoms, PointF position) : this(
        CreateRuns(text, range, context), atoms, position)
    {
    }

    private static List<TextRunDisplay<TFont, TGlyph>> CreateRuns(
        AttributedString<TFont, TGlyph> text,
        Range range,
        TypesettingContext<TFont, TGlyph> context)
    {
        System.ArgumentNullException.ThrowIfNull(text);
        return text.Runs.Select(run => new TextRunDisplay<TFont, TGlyph>(run, range, context)).ToList();
    }

    // We don't implement count as it's not clear if it would refer to runs or atoms.
    public IList<TextRunDisplay<TFont, TGlyph>> Runs { get; } = runs;
    public IReadOnlyList<MathAtom> Atoms { get; } = atoms;
    public IEnumerable<TGlyph> Text => Runs.SelectMany(run => run.Run.Glyphs);

    public void Draw(IGraphicsContext<TFont, TGlyph> context)
    {
        System.ArgumentNullException.ThrowIfNull(context);
        this.DrawBackground(context);
        context.SaveState();
        context.SetTextPosition(Position);
        foreach (var run in Runs)
        {
            run.Draw(context);
        }
        context.RestoreState();
    }
    public PointF Position { get; set; } = position;
    public float Ascent => Runs.CollectionAscent();
    public float Descent => Runs.CollectionDescent();
    public float Width => Runs.CollectionWidth();
    public Range Range => Range.Combine(Runs.Select(r => r.Range));
    public bool HasScript { get; set; }
    public Color? TextColor { get; set; }
    public void SetTextColorRecursive(Color? textColor)
    {
        TextColor ??= textColor;
        foreach (var run in Runs)
        {
            run.SetTextColorRecursive(textColor);
        }
    }
    public Color? BackColor { get; set; }
    public override string ToString() => string.Concat(Runs);
}
