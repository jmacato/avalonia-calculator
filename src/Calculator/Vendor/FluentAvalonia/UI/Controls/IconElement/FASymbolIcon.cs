using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Represents an icon that uses a glyph from the bundled Fluent symbol font.
/// </summary>
public sealed class FASymbolIcon : FAIconElement, IDisposable
{
    public static readonly StyledProperty<FASymbol> SymbolProperty =
        AvaloniaProperty.Register<FASymbolIcon, FASymbol>(nameof(Symbol));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<FASymbolIcon, double>(nameof(FontSize), 18d);

    public FASymbol Symbol
    {
        get => GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public void Dispose()
    {
        ReleaseTextLayout();
        GC.SuppressFinalize(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == FontSizeProperty || change.Property == SymbolProperty)
        {
            ReleaseTextLayout();
            InvalidateMeasure();
        }
        else if (change.Property == ForegroundProperty)
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

        string glyph = char.ConvertFromUtf32((int)Symbol);
        var created = new TextLayout(
            glyph,
            new Typeface(SymbolFontFamily),
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

    private static readonly FontFamily SymbolFontFamily =
        new("avares://FluentAvalonia/Fonts#Symbols");
    private TextLayout? _textLayout;
}
