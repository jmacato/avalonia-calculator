// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAvalonia.Core;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Ports the ConsciousStates storyboards from WinUI's ScrollBar template.
/// The delayed, dependent width/height animation deliberately rerasterizes
/// the rounded thumb on every frame, matching the source template.
/// </summary>
public sealed class WinUiScrollBarMotionHost : Grid
{
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<WinUiScrollBarMotionHost, bool>(nameof(IsExpanded));

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<WinUiScrollBarMotionHost, Orientation>(nameof(Orientation), Orientation.Vertical);

    private static readonly TimeSpan ExpandDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan ContractDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan GeometryDuration = TimeSpan.FromMilliseconds(167);
    private static readonly TimeSpan OpacityDuration = TimeSpan.FromMilliseconds(83);

    private Thumb? _thumb;
    private Rectangle? _track;
    private RepeatButton[] _buttons = [];
    private long _stateChanged;
    private long _motionStarted;
    private double _fromThickness;
    private double _fromOffset;
    private double _fromOpacity;
    private bool _motionBegun;
    private bool _isAnimating;
    private bool _frameRequested;

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty && VisualRoot is not null)
        {
            StartStateChange();
        }
        else if (change.Property == OrientationProperty && VisualRoot is not null)
        {
            ResolveParts();
            ApplyTerminalState(IsExpanded);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ResolveParts();
        if (IsExpanded)
        {
            ApplyTerminalState(false);
            StartStateChange();
        }
        else
        {
            ApplyTerminalState(false);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAnimating = false;
        _frameRequested = false;
        _thumb = null;
        _track = null;
        _buttons = [];
        base.OnDetachedFromVisualTree(e);
    }

    private void ResolveParts()
    {
        _thumb = this.GetVisualDescendants().OfType<Thumb>().FirstOrDefault();
        _track = this.GetVisualDescendants().OfType<Rectangle>().FirstOrDefault(control => control.Name == "TrackRect");
        _buttons = this.GetVisualDescendants().OfType<RepeatButton>().ToArray();
    }

    private void StartStateChange()
    {
        ResolveParts();
        if (_thumb is null)
        {
            ApplyTerminalState(IsExpanded);
            return;
        }

        _stateChanged = Stopwatch.GetTimestamp();
        _motionBegun = false;
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
            ApplyTerminalState(IsExpanded);
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

        TimeSpan elapsed = Stopwatch.GetElapsedTime(_stateChanged);
        TimeSpan delay = IsExpanded ? ExpandDelay : ContractDelay;
        if (elapsed < delay)
        {
            RequestFrame();
            return;
        }

        if (!_motionBegun)
        {
            BeginMotion();
            if (!FAUISettings.AreAnimationsEnabled())
            {
                ApplyTerminalState(IsExpanded);
                return;
            }
        }

        double motionSeconds = Stopwatch.GetElapsedTime(_motionStarted).TotalSeconds;
        double opacityProgress = Math.Clamp(motionSeconds / OpacityDuration.TotalSeconds, 0, 1);
        double geometryProgress = Math.Clamp(motionSeconds / GeometryDuration.TotalSeconds, 0, 1);
        double easedGeometryProgress = ScrollBarSpline(geometryProgress);
        double targetThickness = IsExpanded ? 12 : 8;
        double targetOffset = IsExpanded ? 0 : 2;
        double targetOpacity = IsExpanded ? 1 : 0;

        SetThickness(Lerp(_fromThickness, targetThickness, easedGeometryProgress));
        SetOffset(Lerp(_fromOffset, targetOffset, easedGeometryProgress));
        SetChromeOpacity(Lerp(_fromOpacity, targetOpacity, opacityProgress));

        if (geometryProgress >= 1 && opacityProgress >= 1)
        {
            ApplyTerminalState(IsExpanded);
        }
        else
        {
            RequestFrame();
        }
    }

    private void BeginMotion()
    {
        _motionBegun = true;
        _motionStarted = Stopwatch.GetTimestamp();
        _fromThickness = Orientation == Orientation.Vertical ? _thumb?.Width ?? 8 : _thumb?.Height ?? 8;
        _fromOffset = GetOffset();
        _fromOpacity = _track?.Opacity ?? _buttons.FirstOrDefault()?.Opacity ?? 0;
    }

    private void ApplyTerminalState(bool expanded)
    {
        _isAnimating = false;
        _motionBegun = false;
        SetThickness(expanded ? 12 : 8);
        SetOffset(expanded ? 0 : 2);
        SetChromeOpacity(expanded ? 1 : 0);
    }

    private void SetThickness(double value)
    {
        if (_thumb is null)
        {
            return;
        }

        if (Orientation == Orientation.Vertical)
        {
            _thumb.Width = value;
        }
        else
        {
            _thumb.Height = value;
        }
    }

    private double GetOffset()
    {
        if (_thumb?.RenderTransform is not TranslateTransform transform)
        {
            return IsExpanded ? 2 : 0;
        }

        return Orientation == Orientation.Vertical ? transform.X : transform.Y;
    }

    private void SetOffset(double value)
    {
        if (_thumb is null)
        {
            return;
        }

        if (_thumb.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            _thumb.RenderTransform = transform;
        }

        if (Orientation == Orientation.Vertical)
        {
            transform.X = value;
            transform.Y = 0;
        }
        else
        {
            transform.X = 0;
            transform.Y = value;
        }
    }

    private void SetChromeOpacity(double value)
    {
        if (_track is not null)
        {
            _track.Opacity = value;
        }

        foreach (RepeatButton button in _buttons)
        {
            button.Opacity = value;
        }
    }

    // KeySpline="0,0,0,1": x=u^3 and y=3u^2-2u^3.
    private static double ScrollBarSpline(double progress)
    {
        double parameter = Math.Cbrt(progress);
        return 3 * parameter * parameter - 2 * progress;
    }

    private static double Lerp(double from, double to, double progress)
    {
        return from + (to - from) * progress;
    }
}
