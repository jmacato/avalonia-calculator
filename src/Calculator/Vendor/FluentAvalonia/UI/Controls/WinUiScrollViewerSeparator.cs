// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
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
    private double _fromOpacity;
    private double _targetOpacity;
    private TimeSpan _animationDelay;
    private bool _isAnimating;

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
        WinUiCompositorMotion.SetOpacity(this, 0);
        if (IsExpanded)
        {
            StartStateChange();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAnimating = false;
        WinUiCompositorMotion.SetOpacity(this, IsExpanded ? 1 : 0);
        base.OnDetachedFromVisualTree(e);
    }

    private void StartStateChange()
    {
        _fromOpacity = CurrentOpacity();
        _targetOpacity = IsExpanded ? 1 : 0;
        _animationDelay = IsExpanded ? ExpandDelay : TimeSpan.Zero;
        _stateChanged = Stopwatch.GetTimestamp();
        _isAnimating = true;
        if (!FAUISettings.AreAnimationsEnabled() ||
            !WinUiCompositorMotion.AnimateOpacity(
                this,
                (float)_fromOpacity,
                (float)_targetOpacity,
                Duration,
                new LinearEasing(),
                _animationDelay))
        {
            Complete();
        }
    }

    private double CurrentOpacity()
    {
        if (!_isAnimating)
        {
            return IsExpanded ? 1 : 0;
        }

        double elapsed = Stopwatch.GetElapsedTime(_stateChanged).TotalMilliseconds;
        if (elapsed <= _animationDelay.TotalMilliseconds)
        {
            return _fromOpacity;
        }

        double progress = Math.Clamp(
            (elapsed - _animationDelay.TotalMilliseconds) / Duration.TotalMilliseconds,
            0,
            1);
        return _fromOpacity + ((_targetOpacity - _fromOpacity) * progress);
    }

    private void Complete()
    {
        _isAnimating = false;
        Opacity = IsExpanded ? 1 : 0;
        WinUiCompositorMotion.SetOpacity(this, IsExpanded ? 1 : 0);
    }
}
