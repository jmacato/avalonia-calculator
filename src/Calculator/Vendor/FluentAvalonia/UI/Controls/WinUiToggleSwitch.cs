// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using FluentAvalonia.Core;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Preserves WinUI ToggleSwitch's asymmetric toggle-state opacity storyboard:
/// Off-to-On fades for ControlFasterAnimationDuration; On-to-Off snaps while
/// the knob itself follows RepositionThemeAnimation.
/// </summary>
public sealed class WinUiToggleSwitch : ToggleSwitch
{
    private static readonly TimeSpan FadeInDuration = TimeSpan.FromMilliseconds(83);
    private Control? _outerBorder;
    private Control? _switchKnobBounds;
    private Control? _switchKnobOff;
    private Control? _switchKnobOn;
    private long _fadeStarted;
    private double _outerFrom;
    private double _boundsFrom;
    private double _offFrom;
    private double _onFrom;
    private bool _isFadingIn;
    private bool _frameRequested;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnApplyTemplate(e);
        _isFadingIn = false;
        _frameRequested = false;
        _outerBorder = e.NameScope.Find<Control>("OuterBorder");
        _switchKnobBounds = e.NameScope.Find<Control>("SwitchKnobBounds");
        _switchKnobOff = e.NameScope.Find<Control>("SwitchKnobOff");
        _switchKnobOn = e.NameScope.Find<Control>("SwitchKnobOn");
        ApplySteadyState(IsChecked == true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            ApplyToggleState(change.GetNewValue<bool?>() == true);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _isFadingIn = false;
        _frameRequested = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void ApplyToggleState(bool isOn)
    {
        if (!HasVisualParts())
        {
            return;
        }

        if (!isOn || !FAUISettings.AreAnimationsEnabled() || VisualRoot is null)
        {
            ApplySteadyState(isOn);
            return;
        }

        _outerFrom = _outerBorder!.Opacity;
        _boundsFrom = _switchKnobBounds!.Opacity;
        _offFrom = _switchKnobOff!.Opacity;
        _onFrom = _switchKnobOn!.Opacity;
        _fadeStarted = Stopwatch.GetTimestamp();
        _isFadingIn = true;
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
            ApplySteadyState(IsChecked == true);
            return;
        }

        _frameRequested = true;
        topLevel.RequestAnimationFrame(OnAnimationFrame);
    }

    private void OnAnimationFrame(TimeSpan _)
    {
        _frameRequested = false;
        if (!_isFadingIn || !HasVisualParts())
        {
            return;
        }

        double progress = Math.Clamp(
            Stopwatch.GetElapsedTime(_fadeStarted).TotalSeconds / FadeInDuration.TotalSeconds,
            0,
            1);
        _outerBorder!.Opacity = Lerp(_outerFrom, 0, progress);
        _switchKnobBounds!.Opacity = Lerp(_boundsFrom, 1, progress);
        _switchKnobOff!.Opacity = Lerp(_offFrom, 0, progress);
        _switchKnobOn!.Opacity = Lerp(_onFrom, 1, progress);
        if (progress >= 1)
        {
            _isFadingIn = false;
        }
        else
        {
            RequestFrame();
        }
    }

    private void ApplySteadyState(bool isOn)
    {
        _isFadingIn = false;
        if (!HasVisualParts())
        {
            return;
        }

        _outerBorder!.Opacity = isOn ? 0 : 1;
        _switchKnobBounds!.Opacity = isOn ? 1 : 0;
        _switchKnobOff!.Opacity = isOn ? 0 : 1;
        _switchKnobOn!.Opacity = isOn ? 1 : 0;
    }

    private bool HasVisualParts() =>
        _outerBorder is not null
        && _switchKnobBounds is not null
        && _switchKnobOff is not null
        && _switchKnobOn is not null;

    private static double Lerp(double from, double to, double progress) =>
        from + ((to - from) * progress);
}
