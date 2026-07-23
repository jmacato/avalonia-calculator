// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnApplyTemplate(e);
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

        var easing = new LinearEasing();
        _ = CompositionVisualMotion.AnimateOpacity(_outerBorder!, (float)_outerBorder!.Opacity, 0, FadeInDuration, easing);
        _ = CompositionVisualMotion.AnimateOpacity(_switchKnobBounds!, (float)_switchKnobBounds!.Opacity, 1, FadeInDuration, easing);
        _ = CompositionVisualMotion.AnimateOpacity(_switchKnobOff!, (float)_switchKnobOff!.Opacity, 0, FadeInDuration, easing);
        _ = CompositionVisualMotion.AnimateOpacity(_switchKnobOn!, (float)_switchKnobOn!.Opacity, 1, FadeInDuration, easing);
    }

    private void ApplySteadyState(bool isOn)
    {
        if (!HasVisualParts())
        {
            return;
        }

        _outerBorder!.Opacity = isOn ? 0 : 1;
        _switchKnobBounds!.Opacity = isOn ? 1 : 0;
        _switchKnobOff!.Opacity = isOn ? 0 : 1;
        _switchKnobOn!.Opacity = isOn ? 1 : 0;
        CompositionVisualMotion.SetOpacity(_outerBorder, isOn ? 0 : 1);
        CompositionVisualMotion.SetOpacity(_switchKnobBounds, isOn ? 1 : 0);
        CompositionVisualMotion.SetOpacity(_switchKnobOff, isOn ? 0 : 1);
        CompositionVisualMotion.SetOpacity(_switchKnobOn, isOn ? 1 : 0);
    }

    private bool HasVisualParts() =>
        _outerBorder is not null
        && _switchKnobBounds is not null
        && _switchKnobOff is not null
        && _switchKnobOn is not null;
}
