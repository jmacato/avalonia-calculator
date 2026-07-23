using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Represents an icon that uses a glyph from the specified font.
/// </summary>
public sealed partial class FAFontIcon : FAIconElement, IDisposable
{
    public void Dispose()
    {
        ReleaseTextLayout();
        GC.SuppressFinalize(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);

        if (change.Property == TextElement.FontSizeProperty ||
            change.Property == TextElement.FontFamilyProperty ||
            change.Property == TextElement.FontWeightProperty ||
            change.Property == TextElement.FontStyleProperty ||
            change.Property == GlyphProperty)
        {
            ReleaseTextLayout();
            InvalidateMeasure();
        }
        else if (change.Property == TextElement.ForegroundProperty)
        {
            ReleaseTextLayout();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        ReleaseTextLayout();
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        TextLayout layout = GetTextLayout();
        return new Size(layout.Width, layout.Height);
    }

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        TextLayout layout = GetTextLayout();
        var destination = new Rect(Bounds.Size);
        using (context.PushClip(destination))
        {
            var origin = new Point(
                destination.Center.X - layout.Width * 0.5,
                destination.Center.Y - layout.Height * 0.5);
            layout.Draw(context, origin);
        }
    }

    private TextLayout GetTextLayout()
    {
        TextLayout? current = Volatile.Read(ref _textLayout);
        if (current is not null)
        {
            return current;
        }

        var created = new TextLayout(
            Glyph ?? string.Empty,
            new Typeface(FontFamily, FontStyle, FontWeight),
            FontSize,
            Foreground,
            TextAlignment.Left);
        TextLayout? existing = Interlocked.CompareExchange(ref _textLayout, created, null);
        if (existing is not null)
        {
            created.Dispose();
            return existing;
        }

        return created;
    }

    private void ReleaseTextLayout()
    {
        Interlocked.Exchange(ref _textLayout, null)?.Dispose();
    }

    private TextLayout? _textLayout;
}
