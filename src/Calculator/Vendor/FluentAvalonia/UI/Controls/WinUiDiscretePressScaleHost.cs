// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
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
    private IDisposable? _pressDelay;

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
            return;
        }

        SetScale(1);
        _pressDelay?.Dispose();
        _pressDelay = DispatcherTimer.RunOnce(
            () =>
            {
                _pressDelay = null;
                if (IsPressed)
                {
                    SetScale(0.875);
                }
            },
            PressDelay,
            DispatcherPriority.Render);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _pressDelay?.Dispose();
        _pressDelay = null;
        SetScale(1);
        base.OnDetachedFromVisualTree(e);
    }

    private void SetScale(double scale)
    {
        _pressDelay?.Dispose();
        _pressDelay = null;
        CompositionVisualMotion.SetScale(
            this,
            new Vector3((float)scale, (float)scale, 1));
    }
}
