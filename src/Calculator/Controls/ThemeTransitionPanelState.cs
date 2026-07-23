// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.Core;

namespace CalculatorApp.Controls;

internal sealed class ThemeTransitionPanelState
{
    private const double RepositionEpsilon = 0.01;
    private readonly Dictionary<Control, double> _lastChildPositions = new();
    private readonly Dictionary<Control, (double Offset, long Started, TimeSpan Delay)> _repositionAnimations = new();
    private readonly Panel _panel;
    private IDisposable? _entranceCompletion;
    private EntranceThemeTransition? _entranceTransition;
    private RepositionThemeTransition? _repositionTransition;
    private bool _isEntranceActive;
    private bool _isTracking;
    private bool _isSubscribed;

    internal ThemeTransitionPanelState(Panel panel)
    {
        _panel = panel;
    }

    internal void Prepare(
        EntranceThemeTransition? entranceTransition,
        RepositionThemeTransition? repositionTransition)
    {
        Reset();
        EnsureSubscribed();
        _entranceTransition = entranceTransition;
        _repositionTransition = repositionTransition;
        _isEntranceActive = entranceTransition is not null;
        CaptureChildPositions();

        if (entranceTransition is null)
        {
            return;
        }

        var initialTranslation = new Vector3(
            (float)entranceTransition.FromHorizontalOffset,
            (float)entranceTransition.FromVerticalOffset,
            0);
        foreach (Control child in _panel.Children)
        {
            CompositionVisualMotion.ClearImplicitAnimations(child);
            CompositionVisualMotion.SetTranslation(child, initialTranslation);
            CompositionVisualMotion.SetOpacity(child, 0);
        }
    }

    internal TimeSpan Start()
    {
        if (_entranceTransition is not { } entranceTransition)
        {
            BeginTracking();
            return TimeSpan.Zero;
        }

        int index = 0;
        foreach (Control child in _panel.Children)
        {
            TimeSpan delay = entranceTransition.IsStaggeringEnabled
                ? ThemeTransitionBehavior.GetStaggerDelay(index)
                : TimeSpan.Zero;
            _ = CompositionVisualMotion.AnimateImplicitTranslationAndOpacity(
                child,
                Vector3.Zero,
                (float)child.Opacity,
                ThemeTransitionBehavior.EntranceDuration,
                ThemeTransitionBehavior.ThemeTransitionEasing,
                delay);
            index++;
        }

        TimeSpan maximumDelay = entranceTransition.IsStaggeringEnabled
            ? ThemeTransitionBehavior.GetStaggerDelay(Math.Max(0, _panel.Children.Count - 1))
            : TimeSpan.Zero;
        _entranceCompletion = DispatcherTimer.RunOnce(
            BeginTracking,
            ThemeTransitionBehavior.EntranceDuration + maximumDelay,
            DispatcherPriority.Render);
        return ThemeTransitionBehavior.EntranceDuration + maximumDelay;
    }

    internal void Reset()
    {
        _entranceCompletion?.Dispose();
        _entranceCompletion = null;
        _isEntranceActive = false;
        _isTracking = false;
        _entranceTransition = null;
        _repositionTransition = null;
        foreach (Control child in _panel.Children)
        {
            CompositionVisualMotion.ClearImplicitAnimations(child);
            CompositionVisualMotion.SetTranslation(child, Vector3.Zero);
            CompositionVisualMotion.SetOpacity(child, (float)child.Opacity);
        }

        _repositionAnimations.Clear();
        _lastChildPositions.Clear();
    }

    private void EnsureSubscribed()
    {
        if (_isSubscribed)
        {
            return;
        }

        _panel.LayoutUpdated += OnLayoutUpdated;
        _panel.DetachedFromVisualTree += OnDetachedFromVisualTree;
        _isSubscribed = true;
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        Reset();
        _panel.LayoutUpdated -= OnLayoutUpdated;
        _panel.DetachedFromVisualTree -= OnDetachedFromVisualTree;
        _isSubscribed = false;
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (_isEntranceActive)
        {
            CaptureChildPositions();
            return;
        }

        if (!_isTracking)
        {
            return;
        }

        int index = 0;
        foreach (Control child in _panel.Children)
        {
            double position = child.Bounds.Y;
            if (_lastChildPositions.TryGetValue(child, out double previousPosition))
            {
                double layoutOffset = previousPosition - position;
                if (Math.Abs(layoutOffset) > RepositionEpsilon)
                {
                    BeginReposition(child, layoutOffset, index);
                }
            }

            _lastChildPositions[child] = position;
            index++;
        }
    }

    private void BeginReposition(Control child, double layoutOffset, int index)
    {
        if (_repositionTransition is null || !FAUISettings.AreAnimationsEnabled())
        {
            _repositionAnimations.Remove(child);
            CompositionVisualMotion.SetTranslation(child, Vector3.Zero);
            return;
        }

        double offset = CurrentRepositionOffset(child) + layoutOffset;
        TimeSpan delay = _repositionTransition.IsStaggeringEnabled
            ? ThemeTransitionBehavior.GetStaggerDelay(index)
            : TimeSpan.Zero;
        _repositionAnimations[child] = (offset, Stopwatch.GetTimestamp(), delay);
        _ = CompositionVisualMotion.AnimateTranslation(
            child,
            new Vector3(0, (float)offset, 0),
            Vector3.Zero,
            ThemeTransitionBehavior.RepositionDuration,
            ThemeTransitionBehavior.ThemeTransitionEasing,
            delay);
    }

    private double CurrentRepositionOffset(Control child)
    {
        if (!_repositionAnimations.TryGetValue(child, out var animation))
        {
            return 0;
        }

        double elapsedMilliseconds =
            Stopwatch.GetElapsedTime(animation.Started).TotalMilliseconds -
            animation.Delay.TotalMilliseconds;
        double progress = Math.Clamp(
            elapsedMilliseconds / ThemeTransitionBehavior.RepositionDuration.TotalMilliseconds,
            0,
            1);
        return animation.Offset *
            (1 - ThemeTransitionBehavior.ThemeTransitionEasing.Ease(progress));
    }

    private void BeginTracking()
    {
        _entranceCompletion?.Dispose();
        _entranceCompletion = null;
        _isEntranceActive = false;
        _isTracking = true;
        foreach (Control child in _panel.Children)
        {
            CompositionVisualMotion.ClearImplicitAnimations(child);
        }

        CaptureChildPositions();
    }

    private void CaptureChildPositions()
    {
        _lastChildPositions.Clear();
        foreach (Control child in _panel.Children)
        {
            _lastChildPositions[child] = child.Bounds.Y;
        }
    }
}
