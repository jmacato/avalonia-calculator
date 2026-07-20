// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAvalonia.Core;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Implements the ScrollBar line-arrow's discrete 1 -> 0.875 keyframe at
/// exactly 16 ms. Returning to Normal restores scale immediately.
/// </summary>
public sealed class WinUiDiscretePressScaleHost : Decorator
{
    public static readonly StyledProperty<bool> IsPressedProperty =
        AvaloniaProperty.Register<WinUiDiscretePressScaleHost, bool>(nameof(IsPressed));

    private static readonly TimeSpan PressDelay = TimeSpan.FromMilliseconds(16);
    private long _pressedAt;
    private bool _frameRequested;

    public bool IsPressed
    {
        get => GetValue(IsPressedProperty);
        set => SetValue(IsPressedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property != IsPressedProperty)
        {
            return;
        }

        if (!IsPressed || !FAUISettings.AreAnimationsEnabled())
        {
            SetScale(IsPressed ? 0.875 : 1);
            _frameRequested = false;
            return;
        }

        SetScale(1);
        _pressedAt = Stopwatch.GetTimestamp();
        RequestFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _frameRequested = false;
        SetScale(1);
        base.OnDetachedFromVisualTree(e);
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
        if (!IsPressed)
        {
            SetScale(1);
        }
        else if (Stopwatch.GetElapsedTime(_pressedAt) >= PressDelay)
        {
            SetScale(0.875);
        }
        else
        {
            RequestFrame();
        }
    }

    private void SetScale(double scale)
    {
        if (RenderTransform is not ScaleTransform transform)
        {
            transform = new ScaleTransform();
            RenderTransform = transform;
        }

        transform.ScaleX = scale;
        transform.ScaleY = scale;
    }
}
