// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
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
    private IDisposable? _completionTimer;

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
        _completionTimer?.Dispose();
        _completionTimer = null;
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

        if (!WinUiCompositorMotion.AnimateOpacity(
                this,
                (float)Opacity,
                0,
                Duration,
                new LinearEasing()))
        {
            CompleteDisabledState();
            return;
        }

        _completionTimer?.Dispose();
        _completionTimer = DispatcherTimer.RunOnce(
            CompleteDisabledState,
            Duration,
            DispatcherPriority.Render);
    }

    private void CompleteNormalState()
    {
        _completionTimer?.Dispose();
        _completionTimer = null;
        Fill = NormalFill;
        Opacity = 1;
        WinUiCompositorMotion.SetOpacity(this, 1);
    }

    private void CompleteDisabledState()
    {
        _completionTimer?.Dispose();
        _completionTimer = null;
        if (!IsDisabled)
        {
            return;
        }

        Fill = DisabledFill;
        Opacity = 0;
        WinUiCompositorMotion.SetOpacity(this, 0);
    }
}
