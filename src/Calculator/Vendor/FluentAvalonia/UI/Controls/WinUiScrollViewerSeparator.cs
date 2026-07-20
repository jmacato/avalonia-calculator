// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using FluentAvalonia.Core;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Exact ScrollViewer ScrollBarSeparatorStates opacity timeline. Expansion
/// begins at 400 ms and runs for 100 ms. Avalonia's paired ScrollBars expose
/// their collapsed state after the source-equivalent two-second idle delay,
/// so contraction starts immediately from that notification and runs 100 ms.
/// </summary>
public sealed class WinUiScrollViewerSeparator : Panel
{
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<WinUiScrollViewerSeparator, bool>(nameof(IsExpanded));

    private static readonly TimeSpan ExpandDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(100);
    private long _stateChanged;
    private long _motionStarted;
    private double _fromOpacity;
    private bool _motionBegun;
    private bool _isAnimating;
    private bool _frameRequested;

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty && VisualRoot is not null)
        {
            StartStateChange();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Opacity = 0;
        if (IsExpanded)
        {
            StartStateChange();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAnimating = false;
        _frameRequested = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void StartStateChange()
    {
        _stateChanged = Stopwatch.GetTimestamp();
        _motionBegun = false;
        _isAnimating = true;
        RequestFrame();
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

        TimeSpan delay = IsExpanded ? ExpandDelay : TimeSpan.Zero;
        if (Stopwatch.GetElapsedTime(_stateChanged) < delay)
        {
            RequestFrame();
            return;
        }

        if (!_motionBegun)
        {
            _motionBegun = true;
            _motionStarted = Stopwatch.GetTimestamp();
            _fromOpacity = Opacity;
            if (!FAUISettings.AreAnimationsEnabled())
            {
                Complete();
                return;
            }
        }

        double progress = Math.Clamp(Stopwatch.GetElapsedTime(_motionStarted).TotalSeconds / Duration.TotalSeconds, 0, 1);
        double target = IsExpanded ? 1 : 0;
        Opacity = _fromOpacity + ((target - _fromOpacity) * progress);
        if (progress >= 1)
        {
            Complete();
        }
        else
        {
            RequestFrame();
        }
    }

    private void Complete()
    {
        _isAnimating = false;
        _motionBegun = false;
        Opacity = IsExpanded ? 1 : 0;
    }
}
