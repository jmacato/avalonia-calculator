using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
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
        IBrush? foreground)
    {
        return new FAProgressRingAnimatedVisualCustomCompHandler(minimum, maximum, value, isActive, background, foreground);
    }

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
            _paint.StrokeWidth = _indeterminate ? IndeterminateStrokeThickness : DeterminateStrokeThickness;
            _paint.Color = _background.Value;
            dc.DrawOval(_indeterminate ? IndeterminateBounds : DeterminateBounds, _paint);
        }

        _paint.StrokeWidth = _indeterminate ? IndeterminateStrokeThickness : DeterminateStrokeThickness;
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
            var now = CompositionNow;
            if (!_lastTime.HasValue)
                _lastTime = now;
            double seconds = (now - _lastTime.Value).TotalSeconds % SourceDurationSeconds;
            float progress = (float)(seconds / SourceDurationSeconds);

            // Exact ProgressRingIndeterminate generated Composition program:
            // two half-cycle shapes hand opacity off at 0.5, the leading trim
            // grows from 0.0001 to 0.5, the trailing trim then catches up to
            // 0.5, and the container rotates 0 -> 450 -> 900 degrees. Its
            // cubic control points lie on the diagonal, so the curve is
            // mathematically linear while retaining the exact endpoints.
            float trimStart;
            float trimEnd;
            if (progress < 0.5f)
            {
                trimStart = 0;
                trimEnd = Lerp(0.0000999999975f, 0.5f, progress * 2);
            }
            else
            {
                trimStart = Lerp(0, 0.5f, (progress - 0.5f) * 2);
                trimEnd = 0.5f;
            }

            SetArc(
                IndeterminateBounds,
                -90 + 900 * progress + 360 * trimStart,
                360 * (trimEnd - trimStart));
        }
        else if (_isAnimatingToValue)
        {
            var now = CompositionNow;
            if (!_lastTime.HasValue)
                _lastTime = now;
            double seconds = (now - _lastTime.Value).TotalSeconds;
            double duration = SourceDurationSeconds * Math.Abs(_animationTo - _animationFrom);
            float progress = duration <= 0 ? 1 : (float)(seconds / duration);
            if (progress >= 1)
            {
                _isAnimatingToValue = false;
                _lastTime = null;
                progress = 1;
            }

            _displayProgress = Lerp(_animationFrom, _animationTo, progress);
            SetDeterminateArc(_displayProgress);
        }
        else
        {
            _displayProgress = Normalize(_value);
            SetDeterminateArc(_displayProgress);
        }
    }

    private void SetDeterminateArc(float progress)
    {
        if (progress < 0.00833333377f)
        {
            ReplacePath(new SKPath());
            return;
        }

        float trimEnd = progress switch
        {
            <= 0.00833333377f => 0.0000999999975f,
            <= 0.25f => InterpolateSegment(progress, 0.00833333377f, 0.25f, 0.0000999999975f, 0.25f),
            <= 0.5f => InterpolateSegment(progress, 0.25f, 0.5f, 0.25f, 0.5f),
            <= 0.75f => InterpolateSegment(progress, 0.5f, 0.75f, 0.5f, 0.75f),
            <= 0.983333349f => InterpolateSegment(progress, 0.75f, 0.983333349f, 0.75f, 0.96666666f),
            <= 0.991666675f => InterpolateSegment(progress, 0.983333349f, 0.991666675f, 0.96666666f, 1),
            _ => 1
        };
        SetArc(DeterminateBounds, -90, 360 * trimEnd);
    }

    private void SetArc(SKRect bounds, float startAngle, float sweepAngle)
    {
        using var builder = new SKPathBuilder();
        builder.AddArc(bounds, startAngle, sweepAngle);
        ReplacePath(builder.Detach());
    }

    private void ReplacePath(SKPath path)
    {
        SKPath previous = _path;
        _path = path;
        previous.Dispose();
    }

    private float Normalize(float value)
    {
        float range = _max - _min;
        return range <= 0 ? 0 : Math.Clamp((value - _min) / range, 0, 1);
    }

    private static float InterpolateSegment(float value, float start, float end, float from, float to)
    {
        return Lerp(from, to, (value - start) / (end - start));
    }

    private static float Lerp(float from, float to, float progress)
    {
        return from + (to - from) * progress;
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
                        float previous = _value;
                        if (next <= _value)
                        {
                            _value = next;
                            _isAnimatingToValue = false;
                            _lastTime = null;
                        }
                        else
                        {
                            _animationFrom = Normalize(previous);
                            _value = next;
                            _animationTo = Normalize(next);
                            _isAnimatingToValue = true;
                            _lastTime = null;
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
    private readonly SKPaint _paint;
    private SKPath _path;
    private readonly SKPaint _layerPaint;
    private SKColor? _background;
    private SKColor _foreground;
    private float _min, _max, _value;
    private float _animationFrom, _animationTo, _displayProgress;
    private bool _indeterminate;
    private bool _active;
    private bool _isAnimatingToValue;
    private int _disposeState;

    private const double SourceDurationSeconds = 2;
    private const float IndeterminateSourceScale = 5;
    private const float DeterminateSourceScale = 1.76999998f * 2.5f;
    private const float IndeterminateStrokeThickness = 1.5f * IndeterminateSourceScale;
    private const float DeterminateStrokeThickness = 1.5f * DeterminateSourceScale;
    private static readonly SKRect IndeterminateBounds = CreateCenteredBounds(7 * IndeterminateSourceScale);
    private static readonly SKRect DeterminateBounds = CreateCenteredBounds(8 * DeterminateSourceScale);

    private static SKRect CreateCenteredBounds(float radius)
    {
        return new SKRect(40 - radius, 40 - radius, 40 + radius, 40 + radius);
    }
}
