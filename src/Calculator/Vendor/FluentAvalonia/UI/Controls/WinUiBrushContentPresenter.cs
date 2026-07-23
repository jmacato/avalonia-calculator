// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using FluentAvalonia.Core;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// ContentPresenter host for Windows XAML's compositor-backed BrushTransition.
/// </summary>
public sealed class WinUiBrushContentPresenter : ContentPresenter
{
    private static readonly TimeSpan MinimumDuration = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromDays(24);

    public static readonly StyledProperty<IBrush?> TargetBackgroundProperty =
        AvaloniaProperty.Register<WinUiBrushContentPresenter, IBrush?>(nameof(TargetBackground));

    public static readonly StyledProperty<TimeSpan> TransitionDurationProperty =
        AvaloniaProperty.Register<WinUiBrushContentPresenter, TimeSpan>(
            nameof(TransitionDuration),
            TimeSpan.FromMilliseconds(83));

    private readonly SolidColorBrush _animatedBrush = new();
    private IBrush? _targetBrush;
    private Color _fromColor;
    private Color _toColor;
    private long _started;
    private bool _isAnimating;
    private bool _frameRequested;

    public IBrush? TargetBackground
    {
        get => GetValue(TargetBackgroundProperty);
        set => SetValue(TargetBackgroundProperty, value);
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
        if (change.Property == TargetBackgroundProperty)
        {
            TransitionTo(change.GetOldValue<IBrush?>(), change.GetNewValue<IBrush?>());
        }
        else if (change.Property == TransitionDurationProperty && _isAnimating)
        {
            StartAnimation(_animatedBrush.Color, _toColor, _animatedBrush.Opacity);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Complete(TargetBackground);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Complete(TargetBackground);
        base.OnDetachedFromVisualTree(e);
    }

    private void TransitionTo(IBrush? oldBrush, IBrush? newBrush)
    {
        _targetBrush = newBrush;
        if (VisualRoot is null || !FAUISettings.AreAnimationsEnabled())
        {
            Complete(newBrush);
            return;
        }

        if (newBrush is not ISolidColorBrush newSolid
            || (oldBrush is not null && oldBrush is not ISolidColorBrush))
        {
            Complete(newBrush);
            return;
        }

        Color fromColor;
        if (_isAnimating)
        {
            fromColor = _animatedBrush.Color;
        }
        else if (oldBrush is ISolidColorBrush oldSolid)
        {
            fromColor = oldSolid.Color;
        }
        else
        {
            Color color = newSolid.Color;
            fromColor = Color.FromArgb(0, color.R, color.G, color.B);
        }

        StartAnimation(fromColor, newSolid.Color, newSolid.Opacity);
    }

    private void StartAnimation(Color fromColor, Color toColor, double targetOpacity)
    {
        _fromColor = fromColor;
        _toColor = toColor;
        _animatedBrush.Color = fromColor;
        _animatedBrush.Opacity = targetOpacity;
        Background = _animatedBrush;
        _started = Stopwatch.GetTimestamp();
        _isAnimating = true;
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
            Complete(_targetBrush);
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
            Stopwatch.GetElapsedTime(_started).TotalSeconds / ClampDuration(TransitionDuration).TotalSeconds,
            0,
            1);
        _animatedBrush.Color = Color.FromArgb(
            Interpolate(_fromColor.A, _toColor.A, progress),
            Interpolate(_fromColor.R, _toColor.R, progress),
            Interpolate(_fromColor.G, _toColor.G, progress),
            Interpolate(_fromColor.B, _toColor.B, progress));
        if (progress >= 1)
        {
            Complete(_targetBrush);
        }
        else
        {
            RequestFrame();
        }
    }

    private void Complete(IBrush? brush)
    {
        _isAnimating = false;
        _frameRequested = false;
        _targetBrush = brush;
        Background = brush;
    }

    private static TimeSpan ClampDuration(TimeSpan duration)
    {
        return duration < MinimumDuration
            ? MinimumDuration
            : duration > MaximumDuration
                ? MaximumDuration
                : duration;
    }

    private static byte Interpolate(byte from, byte to, double progress)
    {
        return (byte)Math.Clamp(
            Math.Round(from + (to - from) * progress, MidpointRounding.AwayFromZero),
            byte.MinValue,
            byte.MaxValue);
    }
}
