using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CSharpMath.Atom;
using CSharpMath.Rendering.FrontEnd;
using MathTextAlignment = CSharpMath.Rendering.FrontEnd.TextAlignment;
using MathThickness = CSharpMath.Structures.Thickness;

namespace CSharpMath.Avalonia;

public class MathView : Control, ICSharpMathAPI<MathList, Color>
{
    private static readonly MathPainter s_defaultPainter = new();
    private Point _origin;
    private string? _errorMessage;

    public MathView()
    {
        Styles.Add(new global::Avalonia.Styling.Style(
            global::Avalonia.Styling.Selectors.Is<MathView>)
        {
            Setters =
            {
                new global::Avalonia.Styling.Setter(
                    TextColorProperty,
                    new global::Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(
                        "SystemBaseHighColor"))
            }
        });
    }

    public MathPainter Painter { get; } = new();

    protected override Size MeasureOverride(Size availableSize)
    {
        System.Drawing.RectangleF measurement = Painter.Measure((float)availableSize.Width);
        return new Size(measurement.Width, measurement.Height);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var canvas = new AvaloniaCanvas(context, Bounds.Size);
        Painter.Draw(canvas, TextAlignment, Padding, DisplacementX, DisplacementY);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        bool affectsMeasure = false;
        if (change.Property == GlyphBoxColorProperty)
        {
            Painter.GlyphBoxColor = ((Color glyph, Color textRun)?)change.NewValue;
        }
        else if (change.Property == ContentProperty)
        {
            affectsMeasure = true;
            Painter.Content = (MathList?)change.NewValue;
            if (Painter.ErrorMessage == null)
            {
                LaTeX = Painter.LaTeX;
            }
        }
        else if (change.Property == LaTeXProperty)
        {
            affectsMeasure = true;
            Painter.LaTeX = (string?)change.NewValue;
            Content = Painter.Content;
            SetErrorMessage(Painter.ErrorMessage);
        }
        else if (change.Property == DisplayErrorInlineProperty)
        {
            affectsMeasure = true;
            Painter.DisplayErrorInline = (bool)change.NewValue!;
        }
        else if (change.Property == FontSizeProperty)
        {
            affectsMeasure = true;
            Painter.FontSize = (float)change.NewValue!;
        }
        else if (change.Property == ErrorFontSizeProperty)
        {
            affectsMeasure = true;
            Painter.ErrorFontSize = (float?)change.NewValue;
        }
        else if (change.Property == LocalTypefacesProperty)
        {
            affectsMeasure = true;
            Painter.LocalTypefaces =
                (IEnumerable<GlyphTypeface>)change.NewValue!;
        }
        else if (change.Property == TextColorProperty)
        {
            Painter.TextColor = (Color)change.NewValue!;
        }
        else if (change.Property == HighlightColorProperty)
        {
            Painter.HighlightColor = (Color)change.NewValue!;
        }
        else if (change.Property == ErrorColorProperty)
        {
            Painter.ErrorColor = (Color)change.NewValue!;
        }
        else if (change.Property == MagnificationProperty)
        {
            Painter.Magnification = (float)change.NewValue!;
        }
        else if (change.Property == PaintStyleProperty)
        {
            Painter.PaintStyle = (PaintStyle)change.NewValue!;
        }
        else if (change.Property == LineStyleProperty)
        {
            Painter.LineStyle = (LineStyle)change.NewValue!;
        }

        if (affectsMeasure)
        {
            InvalidateMeasure();
        }

        InvalidateVisual();
    }

    private bool IsPanningActive(global::Avalonia.Input.PointerPoint point) =>
        EnablePanning && point.Properties.IsLeftButtonPressed;

    protected override void OnPointerPressed(global::Avalonia.Input.PointerPressedEventArgs e)
    {
        global::Avalonia.Input.PointerPoint point = e.GetCurrentPoint(this);
        if (IsPanningActive(point))
        {
            _origin = point.Position;
        }

        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(global::Avalonia.Input.PointerEventArgs e)
    {
        global::Avalonia.Input.PointerPoint point = e.GetCurrentPoint(this);
        if (IsPanningActive(point))
        {
            Vector displacement = point.Position - _origin;
            _origin = point.Position;
            DisplacementX += (float)displacement.X;
            DisplacementY += (float)displacement.Y;
        }

        base.OnPointerMoved(e);
    }

    protected override void OnPointerReleased(global::Avalonia.Input.PointerReleasedEventArgs e)
    {
        global::Avalonia.Input.PointerPoint point = e.GetCurrentPoint(this);
        if (IsPanningActive(point))
        {
            _origin = point.Position;
        }

        base.OnPointerReleased(e);
    }

    public bool EnablePanning
    {
        get => GetValue(EnablePanningProperty);
        set => SetValue(EnablePanningProperty, value);
    }

    public static readonly StyledProperty<bool> EnablePanningProperty =
        AvaloniaProperty.Register<MathView, bool>(nameof(EnablePanning));

    public (Color glyph, Color textRun)? GlyphBoxColor
    {
        get => GetValue(GlyphBoxColorProperty);
        set => SetValue(GlyphBoxColorProperty, value);
    }

    public static readonly StyledProperty<(Color glyph, Color textRun)?> GlyphBoxColorProperty =
        AvaloniaProperty.Register<MathView, (Color glyph, Color textRun)?>(
            nameof(GlyphBoxColor),
            s_defaultPainter.GlyphBoxColor);

    public MathList? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public static readonly StyledProperty<MathList?> ContentProperty =
        AvaloniaProperty.Register<MathView, MathList?>(nameof(Content), s_defaultPainter.Content);

    [global::Avalonia.Metadata.Content]
    public string? LaTeX
    {
        get => GetValue(LaTeXProperty);
        set => SetValue(LaTeXProperty, value);
    }

    public static readonly StyledProperty<string?> LaTeXProperty =
        AvaloniaProperty.Register<MathView, string?>(nameof(LaTeX), s_defaultPainter.LaTeX);

    public bool DisplayErrorInline
    {
        get => GetValue(DisplayErrorInlineProperty);
        set => SetValue(DisplayErrorInlineProperty, value);
    }

    public static readonly StyledProperty<bool> DisplayErrorInlineProperty =
        AvaloniaProperty.Register<MathView, bool>(
            nameof(DisplayErrorInline),
            s_defaultPainter.DisplayErrorInline);

    public float FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public static readonly StyledProperty<float> FontSizeProperty =
        AvaloniaProperty.Register<MathView, float>(nameof(FontSize), s_defaultPainter.FontSize);

    public float? ErrorFontSize
    {
        get => GetValue(ErrorFontSizeProperty);
        set => SetValue(ErrorFontSizeProperty, value);
    }

    public static readonly StyledProperty<float?> ErrorFontSizeProperty =
        AvaloniaProperty.Register<MathView, float?>(
            nameof(ErrorFontSize),
            s_defaultPainter.ErrorFontSize);

    public IEnumerable<GlyphTypeface> LocalTypefaces
    {
        get => GetValue(LocalTypefacesProperty);
        set => SetValue(LocalTypefacesProperty, value);
    }

    public static readonly StyledProperty<IEnumerable<GlyphTypeface>> LocalTypefacesProperty =
        AvaloniaProperty.Register<MathView, IEnumerable<GlyphTypeface>>(
            nameof(LocalTypefaces),
            s_defaultPainter.LocalTypefaces);

    public Color TextColor
    {
        get => GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public static readonly StyledProperty<Color> TextColorProperty =
        AvaloniaProperty.Register<MathView, Color>(nameof(TextColor), s_defaultPainter.TextColor);

    public Color HighlightColor
    {
        get => GetValue(HighlightColorProperty);
        set => SetValue(HighlightColorProperty, value);
    }

    public static readonly StyledProperty<Color> HighlightColorProperty =
        AvaloniaProperty.Register<MathView, Color>(
            nameof(HighlightColor),
            s_defaultPainter.HighlightColor);

    public Color ErrorColor
    {
        get => GetValue(ErrorColorProperty);
        set => SetValue(ErrorColorProperty, value);
    }

    public static readonly StyledProperty<Color> ErrorColorProperty =
        AvaloniaProperty.Register<MathView, Color>(nameof(ErrorColor), s_defaultPainter.ErrorColor);

    public MathTextAlignment TextAlignment
    {
        get => GetValue(TextAlignmentProperty);
        set => SetValue(TextAlignmentProperty, value);
    }

    public static readonly StyledProperty<MathTextAlignment> TextAlignmentProperty =
        AvaloniaProperty.Register<MathView, MathTextAlignment>(
            nameof(TextAlignment),
            MathTextAlignment.Center);

    public MathThickness Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    public static readonly StyledProperty<MathThickness> PaddingProperty =
        AvaloniaProperty.Register<MathView, MathThickness>(nameof(Padding));

    public float DisplacementX
    {
        get => GetValue(DisplacementXProperty);
        set => SetValue(DisplacementXProperty, value);
    }

    public static readonly StyledProperty<float> DisplacementXProperty =
        AvaloniaProperty.Register<MathView, float>(nameof(DisplacementX));

    public float DisplacementY
    {
        get => GetValue(DisplacementYProperty);
        set => SetValue(DisplacementYProperty, value);
    }

    public static readonly StyledProperty<float> DisplacementYProperty =
        AvaloniaProperty.Register<MathView, float>(nameof(DisplacementY));

    public float Magnification
    {
        get => GetValue(MagnificationProperty);
        set => SetValue(MagnificationProperty, value);
    }

    public static readonly StyledProperty<float> MagnificationProperty =
        AvaloniaProperty.Register<MathView, float>(
            nameof(Magnification),
            s_defaultPainter.Magnification);

    public PaintStyle PaintStyle
    {
        get => GetValue(PaintStyleProperty);
        set => SetValue(PaintStyleProperty, value);
    }

    public static readonly StyledProperty<PaintStyle> PaintStyleProperty =
        AvaloniaProperty.Register<MathView, PaintStyle>(
            nameof(PaintStyle),
            s_defaultPainter.PaintStyle);

    public LineStyle LineStyle
    {
        get => GetValue(LineStyleProperty);
        set => SetValue(LineStyleProperty, value);
    }

    public static readonly StyledProperty<LineStyle> LineStyleProperty =
        AvaloniaProperty.Register<MathView, LineStyle>(
            nameof(LineStyle),
            s_defaultPainter.LineStyle);

    public string? ErrorMessage => _errorMessage;

    public static readonly DirectProperty<MathView, string?> ErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<MathView, string?>(
            nameof(ErrorMessage),
            view => view.ErrorMessage);

    private void SetErrorMessage(string? value) =>
        SetAndRaise(ErrorMessageProperty, ref _errorMessage, value);
}
