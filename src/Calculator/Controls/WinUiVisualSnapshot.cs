// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace CalculatorApp.Controls;

/// <summary>
/// Freezes Calendar's realized vector primitives into one popup-safe visual
/// without invoking an off-screen or nested compositor render.
/// </summary>
public sealed class WinUiVisualSnapshot : Control
{
    private readonly List<Action<DrawingContext>> _renderers = [];
    private readonly Size _sourceSize;

    private WinUiVisualSnapshot(Visual source)
    {
        _sourceSize = source.Bounds.Size;
        ClipToBounds = true;
        IsHitTestVisible = false;
        AddPrimitive(source, source);
        foreach (Visual visual in source.GetVisualDescendants())
        {
            AddPrimitive(source, visual);
        }
    }

    public static WinUiVisualSnapshot Capture(Visual source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new WinUiVisualSnapshot(source);
    }

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (Action<DrawingContext> render in _renderers)
        {
            render(context);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return _sourceSize.Constrain(availableSize);
    }

    private void AddPrimitive(Visual root, Visual visual)
    {
        if (!visual.IsVisible || visual.Bounds.Width <= 0 || visual.Bounds.Height <= 0)
        {
            return;
        }

        Control? primitive = visual switch
        {
            Border border => CreateBorder(border),
            ContentPresenter presenter => CreateBorder(presenter),
            TextBlock textBlock => CreateText(textBlock),
            Panel panel when panel.Background is not null => new Border { Background = panel.Background },
            _ => null
        };
        if (primitive is null || visual.TranslatePoint(default, root) is not { } position)
        {
            return;
        }

        var size = visual.Bounds.Size;
        primitive.UseLayoutRounding = visual is Layoutable layoutable && layoutable.UseLayoutRounding;
        TextOptions textOptions = TextOptions.GetTextOptions(visual);
        TextOptions.SetTextOptions(primitive, textOptions);
        var renderOptions = new RenderOptions
        {
            BitmapInterpolationMode = RenderOptions.GetBitmapInterpolationMode(visual),
            BitmapBlendingMode = RenderOptions.GetBitmapBlendingMode(visual),
            EdgeMode = RenderOptions.GetEdgeMode(visual),
            RequiresFullOpacityHandling = RenderOptions.GetRequiresFullOpacityHandling(visual)
        };
        RenderOptions.SetBitmapInterpolationMode(primitive, renderOptions.BitmapInterpolationMode);
        RenderOptions.SetBitmapBlendingMode(primitive, renderOptions.BitmapBlendingMode);
        RenderOptions.SetEdgeMode(primitive, renderOptions.EdgeMode);
        RenderOptions.SetRequiresFullOpacityHandling(primitive, renderOptions.RequiresFullOpacityHandling);
        primitive.Measure(size);
        primitive.Arrange(new Rect(size));
        double opacity = GetEffectiveOpacity(root, visual);
        IBrush? opacityMask = visual.OpacityMask;
        IEffect? effect = visual.Effect;
        _renderers.Add(context => RenderPrimitive(
            context,
            primitive,
            position,
            size,
            opacity,
            opacityMask,
            effect,
            textOptions,
            renderOptions));
    }

    private static void RenderPrimitive(
        DrawingContext context,
        Control primitive,
        Point position,
        Size size,
        double opacity,
        IBrush? opacityMask,
        IEffect? effect,
        TextOptions textOptions,
        RenderOptions renderOptions)
    {
        var bounds = new Rect(size);
        using (textOptions != default ? context.PushTextOptions(textOptions) : default(DrawingContext.PushedState?))
        using (renderOptions != default ? context.PushRenderOptions(renderOptions) : default(DrawingContext.PushedState?))
        using (context.PushTransform(Matrix.CreateTranslation(position)))
        using (context.PushOpacity(opacity))
        using (opacityMask is not null ? context.PushOpacityMask(opacityMask, bounds) : default(DrawingContext.PushedState?))
        using (effect is not null ? context.PushEffect(effect, bounds) : default(DrawingContext.PushedState?))
        {
            primitive.Render(context);
        }
    }

    private static Border? CreateBorder(Border source)
    {
        if (source.Background is null && source.BorderBrush is null && source.BoxShadow == default)
        {
            return null;
        }

        return new Border
        {
            Background = source.Background,
            BackgroundSizing = source.BackgroundSizing,
            BorderBrush = source.BorderBrush,
            BorderThickness = source.BorderThickness,
            BoxShadow = source.BoxShadow,
            CornerRadius = source.CornerRadius
        };
    }

    private static Border? CreateBorder(ContentPresenter source)
    {
        if (source.Background is null && source.BorderBrush is null && source.BoxShadow == default)
        {
            return null;
        }

        return new Border
        {
            Background = source.Background,
            BackgroundSizing = source.BackgroundSizing,
            BorderBrush = source.BorderBrush,
            BorderThickness = source.BorderThickness,
            BoxShadow = source.BoxShadow,
            CornerRadius = source.CornerRadius
        };
    }

    private static TextBlock CreateText(TextBlock source)
    {
        return new TextBlock
        {
            Background = source.Background,
            FontFamily = source.FontFamily,
            FontFeatures = source.FontFeatures,
            FontSize = source.FontSize,
            FontStretch = source.FontStretch,
            FontStyle = source.FontStyle,
            FontWeight = source.FontWeight,
            Foreground = source.Foreground,
            LetterSpacing = source.LetterSpacing,
            LineHeight = source.LineHeight,
            MaxLines = source.MaxLines,
            Padding = source.Padding,
            Text = source.Text,
            TextAlignment = source.TextAlignment,
            TextDecorations = source.TextDecorations,
            TextTrimming = source.TextTrimming,
            TextWrapping = source.TextWrapping
        };
    }

    private static double GetEffectiveOpacity(Visual root, Visual visual)
    {
        double opacity = 1;
        for (Visual? current = visual; current is not null; current = current.GetVisualParent())
        {
            opacity *= current.Opacity;
            if (ReferenceEquals(current, root))
            {
                break;
            }
        }

        return opacity;
    }
}
