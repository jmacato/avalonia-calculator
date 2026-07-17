using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using Avalonia.VisualTree;
using SkiaSharp;

namespace FluentAvalonia.UI.Controls.Primitives;
/// <summary>
/// Represents the animated visual source for a <see cref = "FAProgressRing"/>
/// </summary>
/// <remarks>
/// This class is only public for Xaml support in the control template of the ProgressRing
/// </remarks>
public sealed class FAProgressRingAnimatedVisual : Control
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        FAProgressRing parent = this.FindAncestorOfType<FAProgressRing>() ??
            throw new InvalidOperationException("The progress-ring visual requires an FAProgressRing ancestor.");
        bool indeterminate = parent.IsIndeterminate;
        CompositionVisual visual = ElementComposition.GetElementVisual(this) ??
            throw new InvalidOperationException("The progress ring has no composition visual.");
        CompositionCustomVisualHandler handler = FAProgressRingAnimatedVisualCustomCompHandler.Create(
            parent.Minimum,
            parent.Maximum,
            parent.Value,
            parent.IsActive,
            parent.Background,
            parent.Foreground);
        _handler = handler;
        _sfc = visual.Compositor.CreateCustomVisual(handler);
        // The WinUI Animated Visual is 80x80
        _sfc.Size = new Vector(80, 80);
        ElementComposition.SetElementChildVisual(this, _sfc);

        _sfc.SendHandlerMessage(new FAProgressRingAnimatedVisualHandlerMessage(FAProgressRingAnimatedVisualHandlerMessageType.Indeterminate, indeterminate));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_sfc is { } customVisual)
        {
            customVisual.SendHandlerMessage(
                new FAProgressRingAnimatedVisualHandlerMessage(FAProgressRingAnimatedVisualHandlerMessageType.Release));
            ElementComposition.SetElementChildVisual(this, null);
            _sfc = null;
            _handler = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnSizeChanged(e);
        // The progress ring's aspect ratio is preserved, so we constrain to the smallest dimension we have
        var minSize = Math.Min(e.NewSize.Width, e.NewSize.Height);
        // The animated visual is 80x80, we scale the composition visual to scale up or down accordingly
        if (_sfc is { } customVisual)
        {
            customVisual.Scale = new Vector3D(minSize / 80, minSize / 80, 1);
        }
    }

    internal void SetMinimum(double min)
    {
        _sfc?.SendHandlerMessage(new FAProgressRingAnimatedVisualHandlerMessage(FAProgressRingAnimatedVisualHandlerMessageType.Min, (float)min));
    }

    internal void SetMaximum(double max)
    {
        _sfc?.SendHandlerMessage(new FAProgressRingAnimatedVisualHandlerMessage(FAProgressRingAnimatedVisualHandlerMessageType.Max, (float)max));
    }

    internal void SetValue(double val)
    {
        _sfc?.SendHandlerMessage(new FAProgressRingAnimatedVisualHandlerMessage(FAProgressRingAnimatedVisualHandlerMessageType.Value, (float)val));
    }

    internal void SetActive(bool active)
    {
        _sfc?.SendHandlerMessage(new FAProgressRingAnimatedVisualHandlerMessage(FAProgressRingAnimatedVisualHandlerMessageType.Active, active));
    }

    internal void SetIndeterminate(bool indeterminate)
    {
        _sfc?.SendHandlerMessage(new FAProgressRingAnimatedVisualHandlerMessage(FAProgressRingAnimatedVisualHandlerMessageType.Indeterminate, indeterminate));
    }

    internal void SetBackground(IBrush? brush)
    {
        if (brush is ISolidColorBrush scb)
        {
            _sfc?.SendHandlerMessage(new FAProgressRingAnimatedVisualHandlerMessage(FAProgressRingAnimatedVisualHandlerMessageType.Background, scb.Color.ToSKColor()));
        }
        else
        {
            _sfc?.SendHandlerMessage(new FAProgressRingAnimatedVisualHandlerMessage(FAProgressRingAnimatedVisualHandlerMessageType.Background, null));
        }
    }

    internal void SetForeground(IBrush? brush)
    {
        if (brush is ISolidColorBrush scb)
        {
            _sfc?.SendHandlerMessage(new FAProgressRingAnimatedVisualHandlerMessage(FAProgressRingAnimatedVisualHandlerMessageType.Foreground, scb.Color.ToSKColor()));
        }
        else
        {
            _sfc?.SendHandlerMessage(new FAProgressRingAnimatedVisualHandlerMessage(FAProgressRingAnimatedVisualHandlerMessageType.Foreground, SKColors.Transparent));
        }
    }

    private CompositionCustomVisual? _sfc;
    private CompositionCustomVisualHandler? _handler;
}
