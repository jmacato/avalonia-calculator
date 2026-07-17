using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Threading;
using CSharpMath.Display;
using CSharpMath.Structures;
using GlyphTypeface = Avalonia.Media.GlyphTypeface;

namespace CSharpMath.Rendering.FrontEnd
{
    using System.Collections.Generic;
    using System.Linq;
    using BackEnd;

    public abstract class Painter<TCanvas, TContent, TColor> : ICSharpMathAPI<TContent, TColor>, IDisposable where TContent : class
    {
        private static readonly char[] s_lineSeparators = ['\r', '\n'];
        public const float DefaultFontSize = PainterConstants.DefaultFontSize;
        private bool _hasErrorColor;
        private bool _hasTextColor;
        private bool _hasHighlightColor;
        private int _disposed;
        private TColor _errorColor = default!;
        private TColor _textColor = default!;
        private TColor _highlightColor = default!;

        #region Non-redisplaying properties
        /// <summary>
        /// Unit of measure: points;
        /// Defaults to <see cref = "FontSize"/>.
        /// </summary>
        public float? ErrorFontSize { get; set; }
        public bool DisplayErrorInline { get; set; } = true;
        public TColor ErrorColor
        {
            get => _hasErrorColor ? _errorColor : UnwrapColor(Color.FromArgb(255, 0, 0));
            set => (_errorColor, _hasErrorColor) = (value, true);
        }
        public TColor TextColor
        {
            get => _hasTextColor ? _textColor : UnwrapColor(Color.FromArgb(0, 0, 0));
            set => (_textColor, _hasTextColor) = (value, true);
        }
        public TColor HighlightColor
        {
            get => _hasHighlightColor ? _highlightColor : UnwrapColor(Color.FromArgb(0, 0, 0, 0));
            set => (_highlightColor, _hasHighlightColor) = (value, true);
        }
        public (TColor glyph, TColor textRun)? GlyphBoxColor { get; set; }
        public PaintStyle PaintStyle { get; set; } = PaintStyle.Fill;
        public float Magnification { get; set; } = 1;
        public string? ErrorMessage { get; protected set; }
        public abstract IDisplay<MathFontSet, Glyph>? Display { get; protected set; }

        #endregion Non-redisplaying properties
        #region Redisplaying properties
        //_field == private field, __field == property-only field
        protected abstract void SetRedisplay();
        MathFontSet? _fonts;
        protected MathFontSet FontSet => _fonts ?? throw new InvalidOperationException("Set LocalTypefaces to an Avalonia GlyphTypeface with an OpenType MATH table before rendering.");

        float __fontSize = DefaultFontSize;
        /// <summary>Unit of measure: points</summary>
        public float FontSize
        {
            get => __fontSize;
            set
            {
                __fontSize = value;
                if (_fonts != null)
                    _fonts = new MathFontSet(_fonts.Value, value);
                SetRedisplay();
            }
        }

        GlyphTypeface[] __localTypefaces = Array.Empty<GlyphTypeface>();
        public IEnumerable<GlyphTypeface> LocalTypefaces
        {
            get => __localTypefaces;
            set
            {
                GlyphTypeface[] localTypefaces = value?.ToArray() ?? Array.Empty<GlyphTypeface>();
                MathFontSet? replacementFonts = localTypefaces.Length == 0
                    ? null
                    : new MathFontSet(localTypefaces, FontSize);
                MathFontSet? previousFonts = _fonts;
                __localTypefaces = localTypefaces;
                _fonts = replacementFonts;
                Display = null;
                SetRedisplay();
                previousFonts?.Dispose();
            }
        }

        Atom.LineStyle __style = Atom.LineStyle.Display;
        public Atom.LineStyle LineStyle
        {
            get => __style;
            set
            {
                __style = value;
                SetRedisplay();
            }
        }

        TContent? __content;
        public TContent? Content
        {
            get => __content;
            set
            {
                __content = value;
                SetRedisplay();
            }
        }

        public string? LaTeX { get => Content is null ? "" : ContentToLaTeX(Content); set => (Content, ErrorMessage) = LaTeXToContent(value ?? ""); }

        #endregion Redisplaying properties
        #region Methods
        protected abstract Result<TContent> LaTeXToContent(string latex);
        protected abstract string ContentToLaTeX(TContent content);
        public abstract Color WrapColor(TColor color);
        public abstract TColor UnwrapColor(Color color);
        public abstract ICanvas WrapCanvas(TCanvas canvas);
        public virtual RectangleF Measure(float textPainterCanvasWidth)
        {
            UpdateDisplay(textPainterCanvasWidth);
            if (Display != null)
                return new RectangleF(0, -Display.Ascent, Display.Width, Display.Ascent + Display.Descent);
            else
                return RectangleF.Empty;
        }

        protected abstract void UpdateDisplayCore(float textPainterCanvasWidth);
        protected void UpdateDisplay(float textPainterCanvasWidth)
        {
            UpdateDisplayCore(textPainterCanvasWidth);
            if (Display == null && DisplayErrorInline && ErrorMessage != null)
            {
                var font = FontSet;
                if (ErrorFontSize is { } errorSize)
                    font = new MathFontSet(font, errorSize);
                var errorLines = ErrorMessage.Split(s_lineSeparators, StringSplitOptions.RemoveEmptyEntries);
                var runs = new List<Display.Displays.TextRunDisplay<MathFontSet, Glyph>>();
                float y = 0;
                for (var i = 0; i < errorLines.Length; i++)
                {
                    var errorLine = errorLines[i];
                    float x = 0;
                    if (i == errorLines.Length - 1 && errorLines.Length > 1)
                    {
                        var pointer = errorLine.TrimStart(' ');
                        var spaces = errorLine.Length - pointer.Length;
                        var pointerIndentChars = errorLines[i - 1];
                        if (spaces < pointerIndentChars.Length)
                            pointerIndentChars = pointerIndentChars.Remove(spaces);
                        x = TypesettingContext.Instance.GlyphBoundsProvider.GetTypographicWidth(font, new AttributedGlyphRun<MathFontSet, Glyph>(pointerIndentChars, TypesettingContext.Instance.GlyphFinder.FindGlyphs(font, pointerIndentChars), font));
                        errorLine = pointer;
                    }

                    var run = new Display.Displays.TextRunDisplay<MathFontSet, Glyph>(new AttributedGlyphRun<MathFontSet, Glyph>(errorLine, TypesettingContext.Instance.GlyphFinder.FindGlyphs(font, errorLine), font), Atom.Range.Zero, TypesettingContext.Instance);
                    run.SetTextColorRecursive(WrapColor(ErrorColor));
                    y -= run.Ascent;
                    run.Position = new PointF(x, y);
                    y -= run.Descent + run.Run.Glyphs.Max(g => g.Typeface.Typeface.Metrics.LineGap * font.ScaleFor(g.Typeface));
                    runs.Add(run);
                }

                Display = new Display.Displays.TextLineDisplay<MathFontSet, Glyph>(runs, Array.Empty<Atom.MathAtom>(), default);
                Display.SetTextColorRecursive(WrapColor(ErrorColor));
            }
        }

        public abstract void Draw(TCanvas canvas, TextAlignment alignment, Thickness padding = default, float offsetX = 0, float offsetY = 0);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            Display = null;
            __content = null;
            ErrorMessage = null;
            __localTypefaces = Array.Empty<GlyphTypeface>();
            _fonts?.Dispose();
            _fonts = null;
        }

        protected void DrawCore(ICanvas canvas, IDisplay<MathFontSet, Glyph>? display, PointF? position = null)
        {
            ArgumentNullException.ThrowIfNull(canvas);
            if (display != null)
            {
                canvas.Save();
                //invert the canvas vertically: displays are drawn with mathematical coordinates, not graphical coordinates
                canvas.Scale(1, -1);
                canvas.Scale(Magnification, Magnification);
                if (position is { } p)
                    display.Position = new PointF(p.X, p.Y);
                canvas.DefaultColor = WrapColor(TextColor);
                canvas.CurrentColor = WrapColor(HighlightColor);
                canvas.CurrentStyle = PaintStyle;
                var measure = Measure(canvas.Width);
                canvas.FillRect(display.Position.X + measure.X, display.Position.Y - display.Descent, measure.Width, measure.Height);
                canvas.CurrentColor = null;
                static T? Nullable<T>(T nonnull)
                    where T : struct => new T?(nonnull);
                display.Draw(new GraphicsContext(canvas, GlyphBoxColor is var (glyph, textRun) ? Nullable((WrapColor(glyph), WrapColor(textRun))) : null));
                canvas.Restore();
            }
        }
        #endregion Methods
    }
}
