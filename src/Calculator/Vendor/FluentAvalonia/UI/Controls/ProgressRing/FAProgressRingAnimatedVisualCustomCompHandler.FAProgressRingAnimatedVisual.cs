using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using Avalonia.VisualTree;
using SkiaSharp;

namespace FluentAvalonia.UI.Controls.Primitives;

internal sealed class FAProgressRingAnimatedVisualCustomCompHandler : CompositionCustomVisualHandler, IDisposable
{
    private FAProgressRingAnimatedVisualCustomCompHandler(double minimum, double maximum, double value, bool isActive, IBrush? background, IBrush? foreground)
    {
        _min = (float)minimum;
        _max = (float)maximum;
        _value = (float)value;
        _active = isActive;
        if (background is ISolidColorBrush scb)
        {
            _background = scb.Color.ToSKColor();
        }

        if (foreground is ISolidColorBrush scbF)
        {
            _foreground = scbF.Color.ToSKColor();
        }

        _paint = new SKPaint
        {
            IsAntialias = true,
            IsStroke = true,
            StrokeWidth = 4f,
            StrokeCap = SKStrokeCap.Round
        };
        _layerPaint = new SKPaint();
        _path = new SKPath();
    }

    public static FAProgressRingAnimatedVisualCustomCompHandler Create(
        double minimum,
        double maximum,
        double value,
        bool isActive,
        IBrush? background,
        IBrush? foreground) =>
        new(minimum, maximum, value, isActive, background, foreground);

    public override void OnRender(ImmediateDrawingContext drawingContext)
    {
        if (Volatile.Read(ref _disposeState) != 0 || !_active)
            return;

        ISkiaSharpApiLeaseFeature? feature = drawingContext.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (feature is null)
        {
            return;
        }

        using var lease = feature.Lease();
        var dc = lease.SkCanvas;
        // Ensure opacity is clamped between 0.0 and 1.0
        double opacity = Math.Clamp(lease.CurrentOpacity, 0d, 1d);
        bool needsOpacityLayer = opacity < 1d;
        if (needsOpacityLayer)
        {
            _layerPaint.Color = SKColors.White.WithAlpha((byte)(255 * opacity));
            // Save the current canvas state and create a new layer
            dc.SaveLayer(_layerPaint);
        }

        if (_background.HasValue)
        {
            _paint.Color = _background.Value;
            dc.DrawArc(_visualBounds, 0, 360, false, _paint);
        }

        _paint.Color = _foreground;
        dc.DrawPath(_path, _paint);
        if (needsOpacityLayer)
        {
            // Restore the canvas to apply the layer with the specified opacity
            dc.Restore();
        }
    }

    public override void OnAnimationFrameUpdate()
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            return;
        }

        Invalidate();
        Update();
        if (_active && (_indeterminate || _isAnimatingToValue))
            RegisterForNextAnimationFrameUpdate();
    }

    private void Update()
    {
        if (_indeterminate)
        {
            // This timing is determined by platform behavior
            var now = CompositionNow;
            if (!_lastTime.HasValue)
                _lastTime = now;
            var elapsed = now - _lastTime.Value;
            var seconds = elapsed.TotalSeconds;
            if (seconds > _duration)
            {
                while (seconds > _duration)
                {
                    seconds -= _duration;
                }

                _lastTime = now - TimeSpan.FromSeconds(seconds);
            }

            // Size:
            // 0% - 0
            // 25% - 180
            // 75% - 180
            // 100% - 0
            var progress = (float)(seconds / _duration);
            float size = 0, size2 = 0, position = 0;
            if (progress < 0.25)
            {
                size = 180 * (progress / 0.25f);
            }
            else if (progress >= 0.75)
            {
                size = 180 * ((1 - progress) / 0.25f);
            }
            else
            {
                size = 180;
            }

            size2 = size / 2;
            // 3 full rotations complete the animation, 360 * 3 = 1080
            position = 1080 * progress;
            _path.Reset();
            _path.MoveTo(40, 10);
            _path.AddArc(_visualBounds, -90 + (position - size2), size);
        }
        else if (_isAnimatingToValue)
        {
            var now = CompositionNow;
            if (!_lastTime.HasValue)
                _lastTime = now;
            var elapsed = now - _lastTime.Value;
            var seconds = elapsed.TotalSeconds;
            var progress = (float)(seconds / _duration);
            if (progress >= 1)
            {
                _isAnimatingToValue = false;
                _lastTime = null;
                progress = 1;
            }

            var dV = _value - _lastValue;
            var size = _lastValue + (dV * progress);
            _path.Reset();
            _path.MoveTo(40, 10);
            _path.AddArc(_visualBounds, -90, 360 * (size - _min) / (_max - _min));
        }
        else
        {
            _path.Reset();
            _path.MoveTo(40, 10);
            _path.AddArc(_visualBounds, -90, 360 * (_value - _min) / (_max - _min));
        }
    }

    public override void OnMessage(object message)
    {
        if (message is FAProgressRingAnimatedVisualHandlerMessage hm)
        {
            if (hm.MessageType == FAProgressRingAnimatedVisualHandlerMessageType.Release)
            {
                Dispose();
                return;
            }

            if (Volatile.Read(ref _disposeState) != 0)
            {
                return;
            }

            switch (hm.MessageType)
            {
                case FAProgressRingAnimatedVisualHandlerMessageType.Min when hm.Data is float minimum:
                    _min = minimum;
                    break;
                case FAProgressRingAnimatedVisualHandlerMessageType.Max when hm.Data is float maximum:
                    _max = maximum;
                    break;
                case FAProgressRingAnimatedVisualHandlerMessageType.Value when hm.Data is float next:
                    {
                        _lastValue = _value;
                        // No animation if we drop the value
                        if (next <= _value)
                        {
                            _value = next;
                            _isAnimatingToValue = false;
                        }
                        else
                        {
                            // Increasing, animate to new value
                            _value = next;
                            _isAnimatingToValue = true;
                            RegisterForNextAnimationFrameUpdate();
                            return;
                        }
                    }

                    break;
                case FAProgressRingAnimatedVisualHandlerMessageType.Active when hm.Data is bool active:
                    _active = active;
                    if (_active && _indeterminate)
                    {
                        RegisterForNextAnimationFrameUpdate();
                        return;
                    }
                    else
                    {
                        _lastTime = null;
                    }

                    break;
                case FAProgressRingAnimatedVisualHandlerMessageType.Indeterminate when hm.Data is bool indeterminate:
                    _indeterminate = indeterminate;
                    if (_indeterminate && _active)
                    {
                        RegisterForNextAnimationFrameUpdate();
                        return;
                    }
                    else
                    {
                        _lastTime = null;
                    }

                    break;
                case FAProgressRingAnimatedVisualHandlerMessageType.Background:
                    {
                        if (hm.Data is SKColor c)
                        {
                            _background = c;
                        }
                        else
                        {
                            _background = null;
                        }
                    }

                    break;
                case FAProgressRingAnimatedVisualHandlerMessageType.Foreground:
                    {
                        if (hm.Data is SKColor c)
                        {
                            _foreground = c;
                        }
                        else
                        {
                            _foreground = SKColors.Transparent;
                        }
                    }

                    break;
                case FAProgressRingAnimatedVisualHandlerMessageType.Release:
                    break;
            }

            Update();
            Invalidate();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _path.Dispose();
        _paint.Dispose();
        _layerPaint.Dispose();
        GC.SuppressFinalize(this);
    }

    private TimeSpan? _lastTime;
    private float _duration = 2;
    private readonly SKPaint _paint;
    private readonly SKPath _path;
    private readonly SKPaint _layerPaint;
    private readonly SKRect _visualBounds = new SKRect(10, 10, 70, 70);
    private SKColor? _background;
    private SKColor _foreground;
    private float _min, _max, _value;
    private bool _indeterminate;
    private bool _active;
    private bool _isAnimatingToValue;
    private float _lastValue;
    private int _disposeState;
}
