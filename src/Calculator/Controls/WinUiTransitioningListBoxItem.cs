// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using GraphControl;

namespace CalculatorApp.Controls;

public sealed class WinUiTransitioningListBoxItem : ListBoxItem
{
    private static readonly SplineEasing StandardEasing = new(0.1, 0.9, 0.2, 1);
    private static readonly SplineEasing DeleteEasing = new(0.11, 0.5, 0.24, 0.96);
    private readonly AnimationFrameTimer _animationTimer;
    private ScaleTransform? _scale;
    private TranslateTransform? _translation;
    private ITransform? _savedTransform;
    private RelativePoint _savedOrigin;
    private double _savedOpacity = 1;
    private long _scaleStarted;
    private TimeSpan _scaleDelay;
    private TimeSpan _scaleDuration;
    private SplineEasing _scaleEasing = StandardEasing;
    private double _scaleFrom = 1;
    private double _scaleTo = 1;
    private double _opacityFrom = 1;
    private double _opacityTo = 1;
    private long _translationStarted;
    private TimeSpan _translationDelay;
    private TimeSpan _translationDuration;
    private double _translationFrom;
    private double _translationTo;
    private bool _hasSavedVisual;
    private bool _isScaleActive;
    private bool _isTranslationActive;
    private bool _holdScale;
    private bool _holdTranslation;

    public WinUiTransitioningListBoxItem()
    {
        _animationTimer = new AnimationFrameTimer(OnAnimationFrame);
    }

    protected override Type StyleKeyOverride => typeof(ListBoxItem);

    public double CurrentTranslation => _translation?.Y ?? 0;

    public void BeginAddition(long started)
    {
        EnsureTransitionVisuals();
        _scaleStarted = started;
        _scaleDelay = TimeSpan.FromMilliseconds(466);
        _scaleDuration = TimeSpan.FromMilliseconds(333);
        _scaleEasing = StandardEasing;
        _scaleFrom = 0.9;
        _scaleTo = 1;
        _opacityFrom = 0;
        _opacityTo = 1;
        _scale!.ScaleX = _scaleFrom;
        _scale.ScaleY = _scaleFrom;
        Opacity = _opacityFrom;
        _isScaleActive = true;
        _holdScale = false;
        StartTimerOrComplete();
    }

    public void BeginDeletion(long started)
    {
        EnsureTransitionVisuals();
        _scaleStarted = started;
        _scaleDelay = TimeSpan.Zero;
        _scaleDuration = TimeSpan.FromMilliseconds(100);
        _scaleEasing = DeleteEasing;
        _scaleFrom = _scale!.ScaleX;
        _scaleTo = 0.9;
        _opacityFrom = Opacity;
        _opacityTo = 0;
        _isScaleActive = true;
        _holdScale = true;
        StartTimerOrComplete();
    }

    public void BeginTranslation(
        long started,
        double from,
        double to,
        TimeSpan delay,
        TimeSpan duration,
        bool holdEnd)
    {
        EnsureTransitionVisuals();
        _translationStarted = started;
        _translationDelay = delay;
        _translationDuration = duration;
        _translationFrom = from;
        _translationTo = to;
        _translation!.Y = from;
        _isTranslationActive = true;
        _holdTranslation = holdEnd;
        StartTimerOrComplete();
    }

    public void CompleteHeldTranslation()
    {
        _isTranslationActive = false;
        _holdTranslation = false;
        if (_translation is not null)
        {
            _translation.Y = 0;
        }

        TryRestoreVisuals();
    }

    public void ResetTransitionVisuals()
    {
        _animationTimer.Stop();
        _isScaleActive = false;
        _isTranslationActive = false;
        _holdScale = false;
        _holdTranslation = false;
        RestoreVisuals();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animationTimer.Detach();
        _isScaleActive = false;
        _isTranslationActive = false;
        _holdScale = false;
        _holdTranslation = false;
        RestoreVisuals();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnAnimationFrame(TimeSpan timestamp)
    {
        _ = timestamp;
        if (_isScaleActive)
        {
            UpdateScale();
        }

        if (_isTranslationActive)
        {
            UpdateTranslation();
        }

        if (!_isScaleActive && !_isTranslationActive)
        {
            _animationTimer.Stop();
            TryRestoreVisuals();
        }
    }

    private void UpdateScale()
    {
        if (_scale is null)
        {
            _isScaleActive = false;
            return;
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(_scaleStarted);
        if (elapsed < _scaleDelay)
        {
            return;
        }

        double progress = Math.Clamp(
            (elapsed - _scaleDelay).TotalMilliseconds / _scaleDuration.TotalMilliseconds,
            0,
            1);
        double eased = _scaleEasing.Ease(progress);
        double scale = Lerp(_scaleFrom, _scaleTo, eased);
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        Opacity = Lerp(_opacityFrom, _opacityTo, progress);
        if (progress >= 1)
        {
            _isScaleActive = false;
        }
    }

    private void UpdateTranslation()
    {
        if (_translation is null)
        {
            _isTranslationActive = false;
            return;
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(_translationStarted);
        if (elapsed < _translationDelay)
        {
            return;
        }

        double progress = Math.Clamp(
            (elapsed - _translationDelay).TotalMilliseconds / _translationDuration.TotalMilliseconds,
            0,
            1);
        _translation.Y = Lerp(_translationFrom, _translationTo, StandardEasing.Ease(progress));
        if (progress >= 1)
        {
            _isTranslationActive = false;
        }
    }

    private void EnsureTransitionVisuals()
    {
        if (_hasSavedVisual)
        {
            return;
        }

        _savedTransform = RenderTransform;
        _savedOrigin = RenderTransformOrigin;
        _savedOpacity = Opacity;
        _scale = new ScaleTransform();
        _translation = new TranslateTransform();
        var group = new TransformGroup();
        if (_savedTransform is { } existing)
        {
            group.Children.Add(existing as Transform ?? new MatrixTransform(existing.Value));
        }

        group.Children.Add(_scale);
        group.Children.Add(_translation);
        SetCurrentValue(RenderTransformProperty, group);
        SetCurrentValue(RenderTransformOriginProperty, RelativePoint.Center);
        _hasSavedVisual = true;
    }

    private void StartTimerOrComplete()
    {
        if (_animationTimer.Start(this))
        {
            return;
        }

        if (_isScaleActive && _scale is not null)
        {
            _scale.ScaleX = _scaleTo;
            _scale.ScaleY = _scaleTo;
            Opacity = _opacityTo;
            _isScaleActive = false;
        }

        if (_isTranslationActive && _translation is not null)
        {
            _translation.Y = _translationTo;
            _isTranslationActive = false;
        }

        TryRestoreVisuals();
    }

    private void TryRestoreVisuals()
    {
        if (!_isScaleActive && !_isTranslationActive && !_holdScale && !_holdTranslation)
        {
            RestoreVisuals();
        }
    }

    private void RestoreVisuals()
    {
        if (!_hasSavedVisual)
        {
            return;
        }

        SetCurrentValue(RenderTransformProperty, _savedTransform);
        SetCurrentValue(RenderTransformOriginProperty, _savedOrigin);
        Opacity = _savedOpacity;
        _savedTransform = null;
        _scale = null;
        _translation = null;
        _hasSavedVisual = false;
    }

    private static double Lerp(double start, double end, double progress)
    {
        return start + (end - start) * progress;
    }
}
