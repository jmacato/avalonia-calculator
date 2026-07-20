// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAvalonia.Core;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Exact ScrollBar thumb Disabled-state timeline: opacity reaches zero over
/// 83 ms and the disabled fill is assigned discretely at the 83 ms keyframe.
/// Returning to Normal restores both base values immediately.
/// </summary>
public sealed class WinUiScrollBarThumbVisual : Rectangle
{
    public static readonly StyledProperty<IBrush?> NormalFillProperty =
        AvaloniaProperty.Register<WinUiScrollBarThumbVisual, IBrush?>(nameof(NormalFill));

    public static readonly StyledProperty<IBrush?> DisabledFillProperty =
        AvaloniaProperty.Register<WinUiScrollBarThumbVisual, IBrush?>(nameof(DisabledFill));

    public static readonly StyledProperty<bool> IsDisabledProperty =
        AvaloniaProperty.Register<WinUiScrollBarThumbVisual, bool>(nameof(IsDisabled));

    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(83);
    private long _started;
    private double _fromOpacity;
    private bool _isAnimating;
    private bool _frameRequested;

    public IBrush? NormalFill
    {
        get => GetValue(NormalFillProperty);
        set => SetValue(NormalFillProperty, value);
    }

    public IBrush? DisabledFill
    {
        get => GetValue(DisabledFillProperty);
        set => SetValue(DisabledFillProperty, value);
    }

    public bool IsDisabled
    {
        get => GetValue(IsDisabledProperty);
        set => SetValue(IsDisabledProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == IsDisabledProperty)
        {
            if (IsDisabled)
            {
                StartDisabledState();
            }
            else
            {
                CompleteNormalState();
            }
        }
        else if (change.Property == NormalFillProperty && !IsDisabled)
        {
            Fill = NormalFill;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (IsDisabled)
        {
            Fill = DisabledFill;
            Opacity = 0;
        }
        else
        {
            CompleteNormalState();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isAnimating = false;
        _frameRequested = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void StartDisabledState()
    {
        if (VisualRoot is null || !FAUISettings.AreAnimationsEnabled())
        {
            Fill = DisabledFill;
            Opacity = 0;
            return;
        }

        _fromOpacity = Opacity;
        _started = Stopwatch.GetTimestamp();
        _isAnimating = true;
        RequestFrame();
    }

    private void CompleteNormalState()
    {
        _isAnimating = false;
        _frameRequested = false;
        Fill = NormalFill;
        Opacity = 1;
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
        if (!_isAnimating || !IsDisabled)
        {
            return;
        }

        double progress = Math.Clamp(Stopwatch.GetElapsedTime(_started).TotalSeconds / Duration.TotalSeconds, 0, 1);
        Opacity = _fromOpacity * (1 - progress);
        if (progress >= 1)
        {
            _isAnimating = false;
            Fill = DisabledFill;
            Opacity = 0;
        }
        else
        {
            RequestFrame();
        }
    }
}
