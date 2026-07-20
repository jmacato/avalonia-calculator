using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using MathComposer.Avalonia.Layout;
using MathComposer.Avalonia.OpenType;
using MathComposer.Avalonia.Rendering;
using MathComposer.Core;

namespace MathComposer.Avalonia.Controls;

/// <summary>Displays a MathML document with OpenType MATH layout.</summary>
public sealed class MathDisplay : Control
{
    /// <summary>Defines the MathML document source.</summary>
    public static readonly StyledProperty<string> MathMlProperty =
        AvaloniaProperty.Register<MathDisplay, string>(nameof(MathMl), string.Empty);

    /// <summary>Defines the immutable document rendered by the control.</summary>
    public static readonly StyledProperty<MathDocument> DocumentProperty =
        AvaloniaProperty.Register<MathDisplay, MathDocument>(
            nameof(Document),
            MathDocument.Empty,
            validate: static value => value is not null);

    /// <summary>Defines the mathematical em size in device-independent pixels.</summary>
    public static readonly StyledProperty<double> MathFontSizeProperty =
        AvaloniaProperty.Register<MathDisplay, double>(
            nameof(MathFontSize),
            30,
            validate: static value => double.IsFinite(value) && value > 0);

    /// <summary>Defines the formula foreground brush.</summary>
    public static readonly StyledProperty<IBrush> ForegroundProperty =
        AvaloniaProperty.Register<MathDisplay, IBrush>(nameof(Foreground), Brushes.Black);

    /// <summary>Defines the space between the control bounds and mathematical content.</summary>
    public static readonly StyledProperty<Thickness> PaddingProperty =
        AvaloniaProperty.Register<MathDisplay, Thickness>(
            nameof(Padding),
            default,
            validate: static value =>
                double.IsFinite(value.Left) && value.Left >= 0 &&
                double.IsFinite(value.Top) && value.Top >= 0 &&
                double.IsFinite(value.Right) && value.Right >= 0 &&
                double.IsFinite(value.Bottom) && value.Bottom >= 0);

    private static readonly MathSelection EmptySelection = CreateEmptySelection();
    private MathDocument _document = MathDocument.Empty;
    private MathLayoutEngine? _layoutEngine;
    private MathRenderer? _renderer;
    private MathLayoutResult? _layout;

    /// <summary>Initializes a non-interactive mathematical display.</summary>
    public MathDisplay()
    {
        ClipToBounds = true;
        Focusable = false;
    }

    /// <summary>Gets or sets the MathML document source.</summary>
    public string MathMl
    {
        get => GetValue(MathMlProperty);
        set => SetValue(MathMlProperty, value ?? string.Empty);
    }

    /// <summary>Gets or sets the mathematical em size.</summary>
    public double MathFontSize
    {
        get => GetValue(MathFontSizeProperty);
        set => SetValue(MathFontSizeProperty, value);
    }

    /// <summary>Gets or sets the formula foreground.</summary>
    public IBrush Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Gets or sets the space around the mathematical content.</summary>
    public Thickness Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>Gets or sets the immutable document rendered by the control.</summary>
    public MathDocument Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        base.Render(context);
        EnsureLayout();
        if (_layout is null || _document.Root.Children.IsEmpty)
        {
            return;
        }

        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        _renderer!.Render(
            context,
            _layout,
            _document,
            EmptySelection,
            ContentOrigin(),
            Foreground,
            focused: false,
            preeditText: string.Empty,
            renderScaling: scaling);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        _ = availableSize;
        EnsureLayout();
        return _layout is null || _document.Root.Children.IsEmpty
            ? new Size(Padding.Left + Padding.Right, Padding.Top + Padding.Bottom)
            : new Size(
                _layout.Size.Width + Padding.Left + Padding.Right,
                _layout.Size.Height + Padding.Top + Padding.Bottom);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == MathMlProperty)
        {
            LoadMathMl(change.GetNewValue<string>() ?? string.Empty);
        }
        else if (change.Property == DocumentProperty)
        {
            _document = change.GetNewValue<MathDocument>();
            AutomationProperties.SetName(
                this,
                MathInterchange.Serialize(_document, MathTextFormat.UnicodeMath));
            InvalidateMathLayout();
        }
        else if (change.Property == MathFontSizeProperty || change.Property == PaddingProperty)
        {
            InvalidateMathLayout();
        }
        else if (change.Property == ForegroundProperty)
        {
            InvalidateVisual();
        }
    }

    private static OpenTypeMathFont LoadMathFont()
    {
        using Stream stream = AssetLoader.Open(
            new Uri("avares://MathComposer.Avalonia/Assets/Fonts/XCharter-Math.otf"));
        return OpenTypeMathFont.Load(stream);
    }

    private static MathSelection CreateEmptySelection()
    {
        MathPosition position = new([], 0);
        return new MathSelection(position, position);
    }

    private void LoadMathMl(string mathMl)
    {
        MathDocument document = string.IsNullOrWhiteSpace(mathMl)
            ? MathDocument.Empty
            : MathInterchange.Parse(mathMl, MathTextFormat.MathMl).Document;
        SetCurrentValue(DocumentProperty, document);
    }

    private void EnsureLayout()
    {
        if (_layout is not null)
        {
            return;
        }

        if (_layoutEngine is null || _renderer is null)
        {
            _layoutEngine = new MathLayoutEngine(LoadMathFont());
            _renderer = new MathRenderer();
        }

        _layout = _layoutEngine.Layout(_document, MathFontSize);
    }

    private Point ContentOrigin()
    {
        double formulaHeight = _layout?.Size.Height ?? 0;
        double availableHeight = Math.Max(
            0,
            Bounds.Height - Padding.Top - Padding.Bottom);
        return new Point(
            Padding.Left,
            Padding.Top + Math.Max(0, (availableHeight - formulaHeight) / 2));
    }

    private void InvalidateMathLayout()
    {
        _layout = null;
        InvalidateMeasure();
        InvalidateVisual();
    }
}
