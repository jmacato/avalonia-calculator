// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.Core;

namespace CalculatorApp.Controls;

/// <summary>
/// Reproduces Windows XAML's EdgeUIThemeTransition for Calculator's full-screen
/// history and memory flyout content.
/// </summary>
public sealed class WinUiEdgeUiTransition
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(367);
    private static readonly SplineEasing StandardEasing = new(0.1, 0.9, 0.2, 1);
    private Control? _target;
    private IDisposable? _completionTimer;
    private Action? _completed;
    private long _started;
    private double _from;
    private double _to;

    public bool IsRunning => _completionTimer is not null;

    public void Begin(Control target, bool show, bool animate, Action? completed = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        double extent = Math.Max(0, target.Bounds.Height);
        double requestedFrom = show ? extent : 0;
        double current = ReferenceEquals(_target, target) && IsRunning
            ? CurrentTranslation()
            : requestedFrom;
        Cancel();

        _target = target;
        _from = current;
        _to = show ? 0 : extent;
        _completed = completed;
        _started = Stopwatch.GetTimestamp();
        if (!animate || extent <= 0 || Math.Abs(_to - _from) <= double.Epsilon ||
            !WinUiCompositorMotion.AnimateTranslation(
                target,
                new Vector3(0, (float)_from, 0),
                new Vector3(0, (float)_to, 0),
                Duration,
                StandardEasing))
        {
            Complete();
            return;
        }

        _completionTimer = DispatcherTimer.RunOnce(Complete, Duration, DispatcherPriority.Render);
    }

    public void Cancel()
    {
        _completionTimer?.Dispose();
        _completionTimer = null;
        _completed = null;
        RestoreTarget();
    }

    public void Detach()
    {
        Cancel();
    }

    private void Complete()
    {
        _completionTimer?.Dispose();
        _completionTimer = null;
        Action? completed = _completed;
        _completed = null;
        RestoreTarget();
        completed?.Invoke();
    }

    private void RestoreTarget()
    {
        if (_target is not null)
        {
            WinUiCompositorMotion.SetTranslation(_target, Vector3.Zero);
        }

        _target = null;
    }

    private double CurrentTranslation()
    {
        double progress = Math.Clamp(
            Stopwatch.GetElapsedTime(_started).TotalMilliseconds / Duration.TotalMilliseconds,
            0,
            1);
        return _from + ((_to - _from) * StandardEasing.Ease(progress));
    }
}
