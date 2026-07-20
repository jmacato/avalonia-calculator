// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using CalculatorApp.ViewModel.Common;
using FluentAvalonia.Core;
using GraphControl;

namespace CalculatorApp.Controls;

public sealed class FlipButtons : ToggleButton
{
    private const double MaximumPressedScale = 0.97;
    private const double ProjectionDepth = 1000;
    private const double ControlSizeThreshold = 925;
    private static readonly TimeSpan PointerDownDelay = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan PointerUpHold = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan PointerUpDuration = TimeSpan.FromMilliseconds(200);
    private static readonly SplineEasing PointerUpEasing = new(0.1, 0.25, 0.75, 0.9);

    public static readonly StyledProperty<CalculatorButtonId> ButtonIdProperty =
        AvaloniaProperty.Register<FlipButtons, CalculatorButtonId>(nameof(ButtonId));

    private readonly AnimationFrameTimer _pointerAnimationTimer;
    private ScaleTransform? _pressedScale;
    private Rotate3DTransform? _pressedTilt;
    private ITransform? _savedRenderTransform;
    private RelativePoint _savedRenderTransformOrigin;
    private Point _pointerPosition;
    private long _animationStartedTimestamp;
    private double _targetScale = 1;
    private double _targetAngleX;
    private double _targetAngleY;
    private double _returnStartScale = 1;
    private double _returnStartAngleX;
    private double _returnStartAngleY;
    private bool _hasPointerPosition;
    private bool _hasSavedTransform;
    private bool _isPointerDownPending;
    private bool _isPointerReturning;

    public FlipButtons()
    {
        _pointerAnimationTimer = new AnimationFrameTimer(OnPointerAnimationFrame);
        Content = "0";
    }

    public CalculatorButtonId ButtonId
    {
        get => GetValue(ButtonIdProperty);
        set => SetValue(ButtonIdProperty, value);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pointerPosition = e.GetPosition(this);
            _hasPointerPosition = true;
        }

        base.OnPointerPressed(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        System.ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == ButtonIdProperty)
        {
            CommandParameter = change.GetNewValue<CalculatorButtonId>();
        }
        else if (change.Property == IsCheckedProperty)
        {
            Content = change.GetNewValue<bool?>() == true ? "1" : "0";
        }
        else if (change.Property == IsPressedProperty)
        {
            if (change.GetNewValue<bool>())
            {
                BeginPointerDown();
            }
            else
            {
                BeginPointerUp();
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _pointerAnimationTimer.Detach();
        RestoreRenderTransform();
        base.OnDetachedFromVisualTree(e);
    }

    private void BeginPointerDown()
    {
        if (!FAUISettings.AreAnimationsEnabled() || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            RestoreRenderTransform();
            return;
        }

        SaveRenderTransform();
        CreatePressedTransforms();
        CalculatePointerDownTargets();
        _animationStartedTimestamp = Stopwatch.GetTimestamp();
        _isPointerDownPending = true;
        _isPointerReturning = false;
        if (!_pointerAnimationTimer.Start(this))
        {
            ApplyPointerTransform(_targetScale, _targetAngleX, _targetAngleY);
            _isPointerDownPending = false;
        }
    }

    private void BeginPointerUp()
    {
        _hasPointerPosition = false;
        if (!_hasSavedTransform || _pressedScale is null || _pressedTilt is null)
        {
            return;
        }

        if (!FAUISettings.AreAnimationsEnabled())
        {
            RestoreRenderTransform();
            return;
        }

        _returnStartScale = _pressedScale.ScaleX;
        _returnStartAngleX = _pressedTilt.AngleX;
        _returnStartAngleY = _pressedTilt.AngleY;
        _animationStartedTimestamp = Stopwatch.GetTimestamp();
        _isPointerDownPending = false;
        _isPointerReturning = true;
        if (!_pointerAnimationTimer.Start(this))
        {
            RestoreRenderTransform();
        }
    }

    private void OnPointerAnimationFrame(TimeSpan timestamp)
    {
        _ = timestamp;
        TimeSpan elapsed = Stopwatch.GetElapsedTime(_animationStartedTimestamp);
        if (_isPointerDownPending)
        {
            if (elapsed < PointerDownDelay)
            {
                return;
            }

            ApplyPointerTransform(_targetScale, _targetAngleX, _targetAngleY);
            _isPointerDownPending = false;
            _pointerAnimationTimer.Stop();
            return;
        }

        if (!_isPointerReturning || elapsed < PointerUpHold)
        {
            return;
        }

        double progress = Math.Clamp(
            (elapsed - PointerUpHold).TotalMilliseconds / PointerUpDuration.TotalMilliseconds,
            0,
            1);
        double eased = PointerUpEasing.Ease(progress);
        ApplyPointerTransform(
            Lerp(_returnStartScale, 1, eased),
            Lerp(_returnStartAngleX, 0, eased),
            Lerp(_returnStartAngleY, 0, eased));
        if (progress >= 1)
        {
            RestoreRenderTransform();
        }
    }

    private void CalculatePointerDownTargets()
    {
        double normalizedX = _hasPointerPosition
            ? Math.Clamp(_pointerPosition.X / Bounds.Width, 0, 1)
            : 0.5;
        double normalizedY = _hasPointerPosition
            ? Math.Clamp(_pointerPosition.Y / Bounds.Height, 0, 1)
            : 0.5;
        double xMagnitude = Math.Abs(normalizedX - 0.5);
        double yMagnitude = Math.Abs(normalizedY - 0.5);
        double bothAxesValue = 1 - (xMagnitude + yMagnitude);
        _targetScale = Lerp(1, MaximumPressedScale, bothAxesValue);

        // PointerAnimationUsingKeyFrames distributes the angular magnitude
        // between the two axes before sampling the two pointer keyframes.
        double xPointerValue = 0.5 + ((normalizedX - 0.5) / 2);
        double yPointerValue = 0.5 + ((normalizedY - 0.5) / 2);
        double maximumAngleX = GetMaximumAngle(Bounds.Width);
        double maximumAngleY = GetMaximumAngle(Bounds.Height);
        _targetAngleY = Lerp(maximumAngleX, -maximumAngleX, xPointerValue);
        _targetAngleX = Lerp(-maximumAngleY, maximumAngleY, yPointerValue);
    }

    private void SaveRenderTransform()
    {
        if (_hasSavedTransform)
        {
            return;
        }

        _savedRenderTransform = RenderTransform;
        _savedRenderTransformOrigin = RenderTransformOrigin;
        _hasSavedTransform = true;
    }

    private void CreatePressedTransforms()
    {
        _pressedScale = new ScaleTransform();
        _pressedTilt = new Rotate3DTransform { Depth = ProjectionDepth };
        var transformGroup = new TransformGroup();
        transformGroup.Children.Add(_pressedScale);
        transformGroup.Children.Add(_pressedTilt);
        SetCurrentValue(RenderTransformOriginProperty, RelativePoint.Center);
        SetCurrentValue(RenderTransformProperty, transformGroup);
    }

    private void ApplyPointerTransform(double scale, double angleX, double angleY)
    {
        if (_pressedScale is not { } pressedScale || _pressedTilt is not { } pressedTilt)
        {
            return;
        }

        pressedScale.ScaleX = scale;
        pressedScale.ScaleY = scale;
        pressedTilt.AngleX = angleX;
        pressedTilt.AngleY = angleY;
    }

    private void RestoreRenderTransform()
    {
        _pointerAnimationTimer.Stop();
        _isPointerDownPending = false;
        _isPointerReturning = false;
        _pressedScale = null;
        _pressedTilt = null;
        if (!_hasSavedTransform)
        {
            return;
        }

        SetCurrentValue(RenderTransformProperty, _savedRenderTransform);
        SetCurrentValue(RenderTransformOriginProperty, _savedRenderTransformOrigin);
        _savedRenderTransform = null;
        _hasSavedTransform = false;
    }

    private static double GetMaximumAngle(double controlSize) => controlSize > ControlSizeThreshold
        ? 0
        : (-0.00000007 * Math.Pow(controlSize, 3))
          + (0.00015904 * Math.Pow(controlSize, 2))
          - (0.12506463 * controlSize)
          + 35.27311191;

    private static double Lerp(double start, double end, double progress)
    {
        return start + ((end - start) * progress);
    }
}
