// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using FluentAvalonia.Core;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Ellipse whose fill and stroke use Windows XAML's direct-RGB linear color
/// interpolation. A zero duration represents the discrete Normal-state setter.
/// </summary>
public sealed class WinUiAnimatedEllipse : Ellipse
{
    public static readonly StyledProperty<IBrush?> TargetFillProperty =
        AvaloniaProperty.Register<WinUiAnimatedEllipse, IBrush?>(nameof(TargetFill));

    public static readonly StyledProperty<IBrush?> TargetStrokeProperty =
        AvaloniaProperty.Register<WinUiAnimatedEllipse, IBrush?>(nameof(TargetStroke));

    public static readonly StyledProperty<TimeSpan> TransitionDurationProperty =
        AvaloniaProperty.Register<WinUiAnimatedEllipse, TimeSpan>(nameof(TransitionDuration));

    private readonly SolidColorBrush _animatedFill = new();
    private readonly SolidColorBrush _animatedStroke = new();
    private IBrush? _targetFill;
    private IBrush? _targetStroke;
    private Color _fillFrom;
    private Color _fillTo;
    private Color _strokeFrom;
    private Color _strokeTo;
    private long _fillStarted;
    private long _strokeStarted;
    private bool _isFillAnimating;
    private bool _isStrokeAnimating;
    private bool _frameRequested;

    public IBrush? TargetFill
    {
        get => GetValue(TargetFillProperty);
        set => SetValue(TargetFillProperty, value);
    }

    public IBrush? TargetStroke
    {
        get => GetValue(TargetStrokeProperty);
        set => SetValue(TargetStrokeProperty, value);
    }

    public TimeSpan TransitionDuration
    {
        get => GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == TargetFillProperty)
        {
            TransitionTo(true, change.GetOldValue<IBrush?>(), change.GetNewValue<IBrush?>());
        }
        else if (change.Property == TargetStrokeProperty)
        {
            TransitionTo(false, change.GetOldValue<IBrush?>(), change.GetNewValue<IBrush?>());
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Complete(true, TargetFill);
        Complete(false, TargetStroke);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Complete(true, TargetFill);
        Complete(false, TargetStroke);
        base.OnDetachedFromVisualTree(e);
    }

    private void TransitionTo(bool fill, IBrush? oldBrush, IBrush? newBrush)
    {
        if (fill)
        {
            _targetFill = newBrush;
        }
        else
        {
            _targetStroke = newBrush;
        }

        if (VisualRoot is null
            || TransitionDuration <= TimeSpan.Zero
            || !FAUISettings.AreAnimationsEnabled()
            || newBrush is not ISolidColorBrush newSolid
            || (oldBrush is not null && oldBrush is not ISolidColorBrush))
        {
            Complete(fill, newBrush);
            return;
        }

        Color fromColor;
        bool isAnimating = fill ? _isFillAnimating : _isStrokeAnimating;
        if (isAnimating)
        {
            fromColor = fill ? _animatedFill.Color : _animatedStroke.Color;
        }
        else if (oldBrush is ISolidColorBrush oldSolid)
        {
            fromColor = oldSolid.Color;
        }
        else
        {
            Color destination = newSolid.Color;
            fromColor = Color.FromArgb(0, destination.R, destination.G, destination.B);
        }

        Start(fill, fromColor, newSolid.Color, newSolid.Opacity);
    }

    private void Start(bool fill, Color from, Color to, double targetOpacity)
    {
        SolidColorBrush brush = fill ? _animatedFill : _animatedStroke;
        brush.Color = from;
        brush.Opacity = targetOpacity;
        if (fill)
        {
            _fillFrom = from;
            _fillTo = to;
            _fillStarted = Stopwatch.GetTimestamp();
            _isFillAnimating = true;
            Fill = brush;
        }
        else
        {
            _strokeFrom = from;
            _strokeTo = to;
            _strokeStarted = Stopwatch.GetTimestamp();
            _isStrokeAnimating = true;
            Stroke = brush;
        }

        RequestFrame();
    }

    private void RequestFrame()
    {
        if (_frameRequested)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            Complete(true, _targetFill);
            Complete(false, _targetStroke);
            return;
        }

        _frameRequested = true;
        topLevel.RequestAnimationFrame(OnAnimationFrame);
    }

    private void OnAnimationFrame(TimeSpan _)
    {
        _frameRequested = false;
        Advance(true);
        Advance(false);
        if (_isFillAnimating || _isStrokeAnimating)
        {
            RequestFrame();
        }
    }

    private void Advance(bool fill)
    {
        if (!(fill ? _isFillAnimating : _isStrokeAnimating))
        {
            return;
        }

        long started = fill ? _fillStarted : _strokeStarted;
        Color from = fill ? _fillFrom : _strokeFrom;
        Color to = fill ? _fillTo : _strokeTo;
        SolidColorBrush brush = fill ? _animatedFill : _animatedStroke;
        double progress = Math.Clamp(
            Stopwatch.GetElapsedTime(started).TotalSeconds / TransitionDuration.TotalSeconds,
            0,
            1);
        brush.Color = Color.FromArgb(
            Interpolate(from.A, to.A, progress),
            Interpolate(from.R, to.R, progress),
            Interpolate(from.G, to.G, progress),
            Interpolate(from.B, to.B, progress));
        if (progress >= 1)
        {
            Complete(fill, fill ? _targetFill : _targetStroke);
        }
    }

    private void Complete(bool fill, IBrush? brush)
    {
        if (fill)
        {
            _isFillAnimating = false;
            _targetFill = brush;
            Fill = brush;
        }
        else
        {
            _isStrokeAnimating = false;
            _targetStroke = brush;
            Stroke = brush;
        }

        _frameRequested = _isFillAnimating || _isStrokeAnimating ? _frameRequested : false;
    }

    private static byte Interpolate(byte from, byte to, double progress)
    {
        return (byte)Math.Clamp(
            Math.Round(from + (to - from) * progress, MidpointRounding.AwayFromZero),
            byte.MinValue,
            byte.MaxValue);
    }
}
