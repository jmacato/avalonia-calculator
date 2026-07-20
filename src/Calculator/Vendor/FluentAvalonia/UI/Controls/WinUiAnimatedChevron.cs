// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAvalonia.Core;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Exact managed playback of WinUI 2.8's generated
/// AnimatedChevronUpDownSmallVisualSource composition visual.
/// </summary>
public sealed class WinUiAnimatedChevron : FAIconElement
{
    private const double SourceSize = 48;
    private const long SourceDurationTicks = 43_333_333;
    private const string NormalOff = "NormalOff";
    private const int EasingStepThenHold = 0;
    private const int EasingHoldThenStep = 1;
    private const int EasingCubic0 = 2;
    private const int EasingCubic1 = 3;
    private const int EasingCubic2 = 4;
    private const int EasingCubic3 = 5;

    public static readonly StyledProperty<string> StateProperty =
        AvaloniaProperty.Register<WinUiAnimatedChevron, string>(nameof(State), NormalOff);

    private static readonly SplineEasing Cubic0 = new(0.166999996, 0.166999996, 0, 1);
    private static readonly SplineEasing Cubic1 = new(0.850000024, 0, 0.75, 1);
    private static readonly SplineEasing Cubic2 = new(0.349999994, 0, 0, 1);
    private static readonly SplineEasing Cubic3 = new(0.850000024, 0, 0.449999988, 1);

    private static readonly (string From, string To, double Start, double End)[] Segments =
    [
        ("NormalOn", "NormalOff", 0, 0.111730769230769),
        ("NormalOn", "PointerOverOn", 0.115576923076923, 0.150192307692308),
        ("NormalOn", "PressedOn", 0.154038461538462, 0.188653846153846),
        ("NormalOff", "NormalOn", 0.1925, 0.304038461538462),
        ("NormalOff", "PointerOverOff", 0.307884615384615, 0.3425),
        ("NormalOff", "PressedOff", 0.346346153846154, 0.380961538461538),
        ("PointerOverOn", "PointerOverOff", 0.384807692307692, 0.419423076923077),
        ("PointerOverOn", "NormalOn", 0.423269230769231, 0.457884615384615),
        ("PointerOverOn", "PressedOn", 0.461730769230769, 0.496346153846154),
        ("PointerOverOff", "PointerOverOn", 0.500192307692308, 0.534807692307692),
        ("PointerOverOff", "NormalOff", 0.538653846153846, 0.573269230769231),
        ("PointerOverOff", "PressedOff", 0.577115384615385, 0.611730769230769),
        ("PressedOn", "PressedOff", 0.615576923076923, 0.650192307692308),
        ("PressedOn", "PointerOverOff", 0.654038461538462, 0.727115384615385),
        ("PressedOn", "NormalOff", 0.730961538461539, 0.804038461538462),
        ("PressedOff", "PressedOn", 0.807884615384615, 0.8425),
        ("PressedOff", "PointerOverOn", 0.846346153846154, 0.919423076923077),
        ("PressedOff", "NormalOn", 0.923269230769231, 0.996346153846154)
    ];

    private static readonly (double Progress, double Value, int Easing)[] RotationFrames =
    [
        (0, 180, EasingStepThenHold),
        (0.057692308, 180, EasingHoldThenStep),
        (0.0961538479, 0, EasingCubic0),
        (0.115384616, 180, EasingHoldThenStep),
        (0.15384616, 180, EasingHoldThenStep),
        (0.192307696, 0, EasingHoldThenStep),
        (0.25, 0, EasingHoldThenStep),
        (0.288461536, 180, EasingCubic0),
        (0.307692319, 0, EasingHoldThenStep),
        (0.346153855, 0, EasingHoldThenStep),
        (0.384615391, 180, EasingHoldThenStep),
        (0.419230759, 0, EasingCubic0),
        (0.423076928, 180, EasingHoldThenStep),
        (0.461538464, 180, EasingHoldThenStep),
        (0.5, 0, EasingHoldThenStep),
        (0.534615397, 180, EasingCubic0),
        (0.538461566, 0, EasingHoldThenStep),
        (0.576923072, 0, EasingHoldThenStep),
        (0.615384638, 180, EasingHoldThenStep),
        (0.649999976, 0, EasingCubic0),
        (0.653846145, 180, EasingHoldThenStep),
        (0.673076928, 180, EasingHoldThenStep),
        (0.711538434, 0, EasingCubic0),
        (0.730769217, 180, EasingHoldThenStep),
        (0.75, 180, EasingHoldThenStep),
        (0.788461566, 0, EasingCubic0),
        (0.807692289, 0, EasingHoldThenStep),
        (0.842307687, 180, EasingCubic0),
        (0.846153855, 0, EasingHoldThenStep),
        (0.865384638, 0, EasingHoldThenStep),
        (0.903846145, 180, EasingCubic0),
        (0.923076928, 0, EasingHoldThenStep),
        (0.942307711, 0, EasingHoldThenStep),
        (0.980769217, 180, EasingCubic0)
    ];

    private static readonly (double Progress, double X, double Y, int Easing)[] OffsetFrames =
    [
        (0, 24, 24, EasingHoldThenStep),
        (0.0384615399, 24, 26, EasingCubic0),
        (0.057692308, 24, 18, EasingCubic1),
        (0.111538462, 24.1709995, 23.9230003, EasingCubic2),
        (0.115384616, 24, 24, EasingHoldThenStep),
        (0.15384616, 24, 24, EasingHoldThenStep),
        (0.188461542, 24, 26, EasingCubic0),
        (0.192307696, 24, 24, EasingHoldThenStep),
        (0.230769232, 24, 22, EasingCubic0),
        (0.25, 24, 30, EasingCubic3),
        (0.303846151, 24, 24, EasingCubic2),
        (0.346153855, 24, 24, EasingHoldThenStep),
        (0.380769223, 24, 22, EasingCubic0),
        (0.384615391, 24, 24, EasingHoldThenStep),
        (0.461538464, 24, 24, EasingHoldThenStep),
        (0.496153831, 24, 26, EasingCubic0),
        (0.5, 24, 24, EasingHoldThenStep),
        (0.576923072, 24, 24, EasingHoldThenStep),
        (0.61153847, 24, 22, EasingCubic0),
        (0.615384638, 24, 26, EasingHoldThenStep),
        (0.649999976, 24, 22, EasingCubic0),
        (0.653846145, 24, 26, EasingHoldThenStep),
        (0.673076928, 24, 18, EasingCubic1),
        (0.726923048, 24.1709995, 23.9230003, EasingCubic2),
        (0.730769217, 24, 26, EasingHoldThenStep),
        (0.75, 24, 18, EasingCubic1),
        (0.80384618, 24.1709995, 23.9230003, EasingCubic2),
        (0.807692289, 24, 22, EasingHoldThenStep),
        (0.842307687, 24, 26, EasingCubic0),
        (0.846153855, 24, 22, EasingHoldThenStep),
        (0.865384638, 24, 30, EasingCubic3),
        (0.919230759, 24, 24, EasingCubic2),
        (0.923076928, 24, 22, EasingHoldThenStep),
        (0.942307711, 24, 30, EasingCubic3),
        (0.996153831, 24, 24, EasingCubic2)
    ];

    private string _currentState = NormalOff;
    private string? _queuedState;
    private double _sourceProgress = Segments[0].End;
    private double _animationFrom;
    private double _animationTo;
    private long _animationDurationTicks;
    private long _started;
    private bool _isAttached;
    private bool _isAnimating;
    private bool _frameRequested;

    public string State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(16, 16);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
        {
            QueueState(NormalizeState(change.GetNewValue<string>()));
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        CutToState(NormalizeState(State));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _isAttached = false;
        _isAnimating = false;
        _frameRequested = false;
        _queuedState = null;
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Foreground is not { } foreground)
        {
            return;
        }

        double viewScale = Math.Min(Bounds.Width, Bounds.Height) / SourceSize;
        if (viewScale <= 0)
        {
            return;
        }

        double rotation = EvaluateRotation(_sourceProgress) * Math.PI / 180;
        Point offset = EvaluateOffset(_sourceProgress);
        double originX = (Bounds.Width - (SourceSize * viewScale)) * 0.5;
        double originY = (Bounds.Height - (SourceSize * viewScale)) * 0.5;
        Point first = Transform(-3.54299998, -1.79299998, rotation, offset, viewScale, originX, originY);
        Point middle = Transform(0.0199999996, 1.70700002, rotation, offset, viewScale, originX, originY);
        Point last = Transform(3.48900008, -1.71500003, rotation, offset, viewScale, originX, originY);
        var pen = new Pen(foreground, 4 * viewScale, null, PenLineCap.Round, PenLineJoin.Round, 2);
        var geometry = new PathGeometry();
        using (StreamGeometryContext geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(first, false);
            geometryContext.LineTo(middle);
            geometryContext.LineTo(last);
            geometryContext.EndFigure(false);
        }

        using (context.PushClip(new Rect(Bounds.Size)))
        {
            context.DrawGeometry(null, pen, geometry);
        }
    }

    private void QueueState(string state)
    {
        if (!_isAttached)
        {
            CutToState(state);
            return;
        }

        if (_isAnimating)
        {
            if (_queuedState is null)
            {
                _queuedState = state;
            }
            else
            {
                string intermediate = _queuedState;
                _queuedState = state;
                StartTransition(_currentState, intermediate);
            }

            return;
        }

        StartTransition(_currentState, state);
    }

    private void StartTransition(string fromState, string toState)
    {
        if (string.Equals(fromState, toState, StringComparison.Ordinal))
        {
            _currentState = toState;
            DrainQueue();
            return;
        }

        if (!TryGetSegment(fromState, toState, out (string From, string To, double Start, double End) segment))
        {
            CutToState(toState);
            DrainQueue();
            return;
        }

        _currentState = toState;
        _animationFrom = segment.Start;
        _animationTo = segment.End;
        _animationDurationTicks = (long)(SourceDurationTicks * Math.Abs(segment.End - segment.Start));
        _sourceProgress = segment.Start;

        // AnimatedIcon cuts segments shorter than 20 ms and obeys the global
        // system animation policy.
        if (_animationDurationTicks < TimeSpan.FromMilliseconds(20).Ticks
            || !FAUISettings.AreAnimationsEnabled()
            || TopLevel.GetTopLevel(this) is null)
        {
            _sourceProgress = segment.End;
            _isAnimating = false;
            InvalidateVisual();
            DrainQueue();
            return;
        }

        _started = Stopwatch.GetTimestamp();
        _isAnimating = true;
        InvalidateVisual();
        RequestFrame();
    }

    private void CutToState(string state)
    {
        _currentState = state;
        _isAnimating = false;
        _queuedState = null;
        foreach ((string From, string To, double Start, double End) segment in Segments)
        {
            if (string.Equals(segment.To, state, StringComparison.Ordinal))
            {
                _sourceProgress = segment.End;
                InvalidateVisual();
                return;
            }
        }

        _sourceProgress = 0;
        InvalidateVisual();
    }

    private void DrainQueue()
    {
        if (_queuedState is not { } queued)
        {
            return;
        }

        _queuedState = null;
        StartTransition(_currentState, queued);
    }

    private void RequestFrame()
    {
        if (_frameRequested || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        _frameRequested = true;
        topLevel.RequestAnimationFrame(OnAnimationFrame);
    }

    private void OnAnimationFrame(TimeSpan _)
    {
        _frameRequested = false;
        if (!_isAnimating)
        {
            return;
        }

        double progress = Math.Clamp(
            Stopwatch.GetElapsedTime(_started).Ticks / (double)_animationDurationTicks,
            0,
            1);
        _sourceProgress = Lerp(_animationFrom, _animationTo, progress);
        InvalidateVisual();
        if (progress >= 1)
        {
            _isAnimating = false;
            DrainQueue();
        }
        else
        {
            RequestFrame();
        }
    }

    private static bool TryGetSegment(
        string from,
        string to,
        out (string From, string To, double Start, double End) result)
    {
        foreach ((string From, string To, double Start, double End) segment in Segments)
        {
            if (string.Equals(segment.From, from, StringComparison.Ordinal)
                && string.Equals(segment.To, to, StringComparison.Ordinal))
            {
                result = segment;
                return true;
            }
        }

        result = default;
        return false;
    }

    private static double EvaluateRotation(double progress) => Evaluate(
        RotationFrames,
        progress,
        static frame => frame.Progress,
        static frame => frame.Value,
        static frame => frame.Easing);

    private static Point EvaluateOffset(double progress) => new(
        Evaluate(OffsetFrames, progress, static frame => frame.Progress, static frame => frame.X, static frame => frame.Easing),
        Evaluate(OffsetFrames, progress, static frame => frame.Progress, static frame => frame.Y, static frame => frame.Easing));

    private static double Evaluate<T>(
        T[] frames,
        double progress,
        Func<T, double> getProgress,
        Func<T, double> getValue,
        Func<T, int> getEasing)
    {
        if (progress <= getProgress(frames[0]))
        {
            return getValue(frames[0]);
        }

        for (int index = 1; index < frames.Length; index++)
        {
            T next = frames[index];
            double nextProgress = getProgress(next);
            if (progress > nextProgress)
            {
                continue;
            }

            T previous = frames[index - 1];
            double interval = nextProgress - getProgress(previous);
            double amount = interval <= 0 ? 1 : (progress - getProgress(previous)) / interval;
            double eased = Ease(getEasing(next), amount);
            return Lerp(getValue(previous), getValue(next), eased);
        }

        return getValue(frames[^1]);
    }

    private static double Ease(int easing, double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        return easing switch
        {
            EasingHoldThenStep => progress < 1 ? 0 : 1,
            EasingStepThenHold => progress <= 0 ? 0 : 1,
            EasingCubic0 => Cubic0.Ease(progress),
            EasingCubic1 => Cubic1.Ease(progress),
            EasingCubic2 => Cubic2.Ease(progress),
            EasingCubic3 => Cubic3.Ease(progress),
            _ => progress
        };
    }

    private static Point Transform(
        double x,
        double y,
        double rotation,
        Point offset,
        double viewScale,
        double originX,
        double originY)
    {
        x *= 4;
        y *= 4;
        double cosine = Math.Cos(rotation);
        double sine = Math.Sin(rotation);
        double transformedX = (x * cosine) - (y * sine) + offset.X;
        double transformedY = (x * sine) + (y * cosine) + offset.Y;
        return new Point(originX + (transformedX * viewScale), originY + (transformedY * viewScale));
    }

    private static string NormalizeState(string? state) => string.IsNullOrEmpty(state) ? NormalOff : state;

    private static double Lerp(double from, double to, double progress) => from + ((to - from) * progress);

}
