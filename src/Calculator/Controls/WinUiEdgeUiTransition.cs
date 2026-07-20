// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using GraphControl;

namespace CalculatorApp.Controls;

/// <summary>
/// Reproduces Windows XAML's EdgeUIThemeTransition for Calculator's full-screen
/// history and memory flyout content.
/// </summary>
public sealed class WinUiEdgeUiTransition
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(367);
    private static readonly SplineEasing StandardEasing = new(0.1, 0.9, 0.2, 1);
    private readonly AnimationFrameTimer _animationTimer;
    private Control? _target;
    private TranslateTransform? _translation;
    private ITransform? _savedTransform;
    private Action? _completed;
    private long _started;
    private double _from;
    private double _to;

    public WinUiEdgeUiTransition()
    {
        _animationTimer = new AnimationFrameTimer(OnAnimationFrame);
    }

    public bool IsRunning => _animationTimer.IsRunning;

    public void Begin(Control target, bool show, bool animate, Action? completed = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        double extent = Math.Max(0, target.Bounds.Height);
        double requestedFrom = show ? extent : 0;
        double current = ReferenceEquals(_target, target) && _translation is not null
            ? _translation.Y
            : requestedFrom;
        Cancel();

        _target = target;
        _savedTransform = target.RenderTransform;
        _translation = new TranslateTransform(0, current);
        var transforms = new TransformGroup();
        if (_savedTransform is { } existing)
        {
            transforms.Children.Add(existing as Transform ?? new MatrixTransform(existing.Value));
        }

        transforms.Children.Add(_translation);
        target.SetCurrentValue(Visual.RenderTransformProperty, transforms);
        _from = current;
        _to = show ? 0 : extent;
        _completed = completed;
        _started = Stopwatch.GetTimestamp();
        if (!animate || extent <= 0 || Math.Abs(_to - _from) <= double.Epsilon || !_animationTimer.Start(target))
        {
            Complete();
        }
    }

    public void Cancel()
    {
        _animationTimer.Stop();
        _completed = null;
        RestoreTarget();
    }

    public void Detach()
    {
        _animationTimer.Detach();
        _completed = null;
        RestoreTarget();
    }

    private void OnAnimationFrame(TimeSpan timestamp)
    {
        _ = timestamp;
        if (_translation is null)
        {
            Complete();
            return;
        }

        double progress = Math.Clamp(Stopwatch.GetElapsedTime(_started).TotalMilliseconds / Duration.TotalMilliseconds, 0, 1);
        _translation.Y = _from + ((_to - _from) * StandardEasing.Ease(progress));
        if (progress >= 1)
        {
            Complete();
        }
    }

    private void Complete()
    {
        if (_translation is not null)
        {
            _translation.Y = _to;
        }

        _animationTimer.Stop();
        Action? completed = _completed;
        _completed = null;
        RestoreTarget();
        completed?.Invoke();
    }

    private void RestoreTarget()
    {
        if (_target is not null)
        {
            _target.SetCurrentValue(Visual.RenderTransformProperty, _savedTransform);
        }

        _target = null;
        _translation = null;
        _savedTransform = null;
    }
}
