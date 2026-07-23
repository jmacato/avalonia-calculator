// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using FluentAvalonia.Core;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Reproduces Windows XAML's compositor-backed BrushTransition for a Border background.
/// </summary>
public sealed class WinUiBrushBorder : Border
{
    private static readonly TimeSpan MinimumDuration = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromDays(24);

    public static readonly StyledProperty<IBrush?> TargetBackgroundProperty =
        AvaloniaProperty.Register<WinUiBrushBorder, IBrush?>(nameof(TargetBackground));

    public static readonly StyledProperty<IBrush?> TargetBorderBrushProperty =
        AvaloniaProperty.Register<WinUiBrushBorder, IBrush?>(nameof(TargetBorderBrush));

    public static readonly StyledProperty<TimeSpan> TransitionDurationProperty =
        AvaloniaProperty.Register<WinUiBrushBorder, TimeSpan>(
            nameof(TransitionDuration),
            TimeSpan.FromMilliseconds(83));

    private readonly SolidColorBrush _animatedBrush = new();
    private readonly SolidColorBrush _animatedBorderBrush = new();
    private IBrush? _targetBrush;
    private IBrush? _targetBorderBrush;
    private Color _fromColor;
    private Color _toColor;
    private Color _borderFromColor;
    private Color _borderToColor;
    private double _targetOpacity;
    private double _targetBorderOpacity;
    private long _started;
    private long _borderStarted;
    private bool _isAnimating;
    private bool _isBorderAnimating;
    private bool _frameRequested;

    public IBrush? TargetBackground
    {
        get => GetValue(TargetBackgroundProperty);
        set => SetValue(TargetBackgroundProperty, value);
    }

    public IBrush? TargetBorderBrush
    {
        get => GetValue(TargetBorderBrushProperty);
        set => SetValue(TargetBorderBrushProperty, value);
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
            IBrush? oldBrush = change.GetOldValue<IBrush?>();
            IBrush? newBrush = change.GetNewValue<IBrush?>();
            TransitionTo(oldBrush, newBrush);
        }
        else if (change.Property == TargetBorderBrushProperty)
        {
            IBrush? oldBrush = change.GetOldValue<IBrush?>();
            IBrush? newBrush = change.GetNewValue<IBrush?>();
            TransitionBorderTo(oldBrush, newBrush);
        }
        else if (change.Property == TransitionDurationProperty)
        {
            if (TransitionDuration <= TimeSpan.Zero)
            {
                if (_isAnimating)
                {
                    Complete(_targetBrush);
                }

                if (_isBorderAnimating)
                {
                    CompleteBorder(_targetBorderBrush);
                }
            }
            else
            {
                if (_isAnimating)
                {
                    // WinUI updates the reusable key-frame animation's duration when a
                    // handoff supplies a different BrushTransition duration.
                    StartAnimation(_animatedBrush.Color, _toColor, _targetOpacity);
                }

                if (_isBorderAnimating)
                {
                    StartBorderAnimation(_animatedBorderBrush.Color, _borderToColor, _targetBorderOpacity);
                }
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Complete(TargetBackground);
        if (TargetBorderBrush is not null)
        {
            CompleteBorder(TargetBorderBrush);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Complete(TargetBackground);
        if (TargetBorderBrush is not null || _isBorderAnimating)
        {
            CompleteBorder(TargetBorderBrush);
        }
        base.OnDetachedFromVisualTree(e);
    }

    private void TransitionTo(IBrush? oldBrush, IBrush? newBrush)
    {
        _targetBrush = newBrush;

        if (VisualRoot is null || TransitionDuration <= TimeSpan.Zero || !FAUISettings.AreAnimationsEnabled())
        {
            Complete(newBrush);
            return;
        }

        // WUCBrushManager permits a non-null SolidColorBrush destination and
        // either a null or SolidColorBrush source. Other brush kinds snap.
        if (newBrush is not ISolidColorBrush newSolid
            || (oldBrush is not null && oldBrush is not ISolidColorBrush))
        {
            Complete(newBrush);
            return;
        }

        Color fromColor;
        if (_isAnimating)
        {
            // Composition replaces the running animation and lets
            // this.StartingValue resolve to the current animated color.
            fromColor = _animatedBrush.Color;
        }
        else if (oldBrush is ISolidColorBrush oldSolid)
        {
            fromColor = oldSolid.Color;
        }
        else
        {
            // A null source fades from the destination RGB with zero alpha.
            Color color = newSolid.Color;
            fromColor = Color.FromArgb(0, color.R, color.G, color.B);
        }

        StartAnimation(fromColor, newSolid.Color, newSolid.Opacity);
    }

    private void StartAnimation(Color fromColor, Color toColor, double targetOpacity)
    {
        _fromColor = fromColor;
        _toColor = toColor;
        _targetOpacity = targetOpacity;
        _animatedBrush.Color = fromColor;
        // Brush.Opacity is not part of WUC's Color key-frame animation. The
        // newly assigned XAML brush opacity participates in the render walk
        // immediately while only its ARGB Color interpolates.
        _animatedBrush.Opacity = targetOpacity;
        Background = _animatedBrush;
        _started = Stopwatch.GetTimestamp();
        _isAnimating = true;
        RequestFrame();
    }

    private void TransitionBorderTo(IBrush? oldBrush, IBrush? newBrush)
    {
        _targetBorderBrush = newBrush;

        if (VisualRoot is null || TransitionDuration <= TimeSpan.Zero || !FAUISettings.AreAnimationsEnabled())
        {
            CompleteBorder(newBrush);
            return;
        }

        if (newBrush is not ISolidColorBrush newSolid
            || (oldBrush is not null && oldBrush is not ISolidColorBrush))
        {
            CompleteBorder(newBrush);
            return;
        }

        Color fromColor;
        if (_isBorderAnimating)
        {
            fromColor = _animatedBorderBrush.Color;
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

        StartBorderAnimation(fromColor, newSolid.Color, newSolid.Opacity);
    }

    private void StartBorderAnimation(Color fromColor, Color toColor, double targetOpacity)
    {
        _borderFromColor = fromColor;
        _borderToColor = toColor;
        _targetBorderOpacity = targetOpacity;
        _animatedBorderBrush.Color = fromColor;
        _animatedBorderBrush.Opacity = targetOpacity;
        BorderBrush = _animatedBorderBrush;
        _borderStarted = Stopwatch.GetTimestamp();
        _isBorderAnimating = true;
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
            AdvanceBorder();
        }
        else
        {
            double elapsedSeconds = Stopwatch.GetElapsedTime(_started).TotalSeconds;
            double durationSeconds = ClampDuration(TransitionDuration).TotalSeconds;
            double progress = Math.Clamp(elapsedSeconds / durationSeconds, 0, 1);

            _animatedBrush.Color = Color.FromArgb(
                Interpolate(_fromColor.A, _toColor.A, progress),
                Interpolate(_fromColor.R, _toColor.R, progress),
                Interpolate(_fromColor.G, _toColor.G, progress),
                Interpolate(_fromColor.B, _toColor.B, progress));
            if (progress >= 1)
            {
                Complete(_targetBrush);
            }

            AdvanceBorder();
        }

        if (_isAnimating || _isBorderAnimating)
        {
            RequestFrame();
        }
    }

    private void AdvanceBorder()
    {
        if (!_isBorderAnimating)
        {
            return;
        }

        double elapsedSeconds = Stopwatch.GetElapsedTime(_borderStarted).TotalSeconds;
        double durationSeconds = ClampDuration(TransitionDuration).TotalSeconds;
        double progress = Math.Clamp(elapsedSeconds / durationSeconds, 0, 1);
        _animatedBorderBrush.Color = Color.FromArgb(
            Interpolate(_borderFromColor.A, _borderToColor.A, progress),
            Interpolate(_borderFromColor.R, _borderToColor.R, progress),
            Interpolate(_borderFromColor.G, _borderToColor.G, progress),
            Interpolate(_borderFromColor.B, _borderToColor.B, progress));
        if (progress >= 1)
        {
            CompleteBorder(_targetBorderBrush);
        }
    }

    private void Complete(IBrush? brush)
    {
        _isAnimating = false;
        _frameRequested = false;
        _targetBrush = brush;
        Background = brush;
    }

    private void CompleteBorder(IBrush? brush)
    {
        _isBorderAnimating = false;
        _targetBorderBrush = brush;
        BorderBrush = brush;
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
        return (byte)Math.Clamp(Math.Round(Lerp(from, to, progress), MidpointRounding.AwayFromZero), byte.MinValue,
            byte.MaxValue);
    }

    private static double Lerp(double from, double to, double progress)
    {
        return from + (to - from) * progress;
    }
}
