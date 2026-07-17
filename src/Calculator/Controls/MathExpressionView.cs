// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using CSharpMath.Avalonia;

namespace CalculatorApp.Controls;
/// <summary>
/// Read-only mathematical layout used where the native app uses a read-only
/// MathRichEditBox. CSharpMath supplies TeX layout while the embedded XCharter
/// Math face supplies the OpenType MATH metrics and Avalonia renders the glyphs.
/// </summary>
public sealed class MathExpressionView : MathView, IDisposable
{
    public static readonly StyledProperty<string> ExpressionProperty = AvaloniaProperty.Register<MathExpressionView, string>(nameof(Expression), string.Empty);
    private static readonly GlyphTypeface[] s_mathTypefaces = LoadMathTypefaces();
    private bool _disposed;
    static MathExpressionView()
    {
        ExpressionProperty.Changed.AddClassHandler<MathExpressionView>(static (view, args) => view.UpdateExpression(args.NewValue as string ?? string.Empty));
    }

    public MathExpressionView()
    {
        LocalTypefaces = s_mathTypefaces;
        DisplayErrorInline = true;
        Focusable = false;
    }

    public string Expression { get => GetValue(ExpressionProperty); set => SetValue(ExpressionProperty, value ?? string.Empty); }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Expression = string.Empty;
        Painter.Dispose();
    }

    private static GlyphTypeface[] LoadMathTypefaces()
    {
        var family = new FontFamily("avares://Calculator/Assets/Fonts/XCharterMath#XCharter Math");
        if (!FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out GlyphTypeface? typeface))
        {
            throw new InvalidDataException("The embedded XCharter Math font could not be loaded.");
        }

        if (!typeface.PlatformTypeface.TryGetTable(new OpenTypeTag('M', 'A', 'T', 'H'), out _))
        {
            throw new InvalidDataException("The embedded XCharter Math font has no OpenType MATH table.");
        }

        return [typeface];
    }

    private void UpdateExpression(string expression)
    {
        LaTeX = GraphMathExpressionFormatter.ToLaTeX(expression);
        AutomationProperties.SetName(this, expression);
    }
}
