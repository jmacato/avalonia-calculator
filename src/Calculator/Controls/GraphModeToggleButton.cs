// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using FluentAvalonia.Core;

namespace CalculatorApp.Controls;

/// <summary>
/// Ports the graph-mode ToggleSwitch transition from Windows Calculator.
/// </summary>
public sealed class GraphModeToggleButton : ToggleButton
{
    private const double OnTranslation = 32;
    private static readonly TimeSpan KnobDuration = TimeSpan.FromMilliseconds(367);
    private static readonly TimeSpan IconDuration = TimeSpan.FromMilliseconds(200);
    private static readonly SplineEasing KnobEasing = new(0.1, 0.9, 0.2, 1);
    private Border? _knob;
    private Panel? _iconsPanelOff;
    private Panel? _iconsPanelOn;
    private long _animationStarted;
    private double _knobFrom;
    private double _knobTo;
    private double _offOpacityFrom;
    private double _offOpacityTo;
    private double _onOpacityFrom;
    private double _onOpacityTo;
    private bool _isAnimating;
    private double _currentKnobTranslation;
    private double _currentOffOpacity = 1;
    private double _currentOnOpacity;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnApplyTemplate(e);
        _knob = e.NameScope.Find<Border>("SwitchKnob");
        _iconsPanelOff = e.NameScope.Find<Panel>("IconsPanelOff");
        _iconsPanelOn = e.NameScope.Find<Panel>("IconsPanelOn");
        if (_knob is null || _iconsPanelOff is null || _iconsPanelOn is null)
        {
            return;
        }

        ApplySteadyState(IsChecked == true);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == IsCheckedProperty)
        {
            AnimateToggle(change.GetNewValue<bool?>() == true);
        }
    }

    private void AnimateToggle(bool isOn)
    {
        if (_knob is null || _iconsPanelOff is null || _iconsPanelOn is null)
        {
            return;
        }

        double targetTranslation = isOn ? OnTranslation : 0;
        double targetOffOpacity = isOn ? 0 : 1;
        double targetOnOpacity = isOn ? 1 : 0;
        if (!FAUISettings.AreAnimationsEnabled() || VisualRoot is null)
        {
            ApplySteadyState(isOn);
            return;
        }

        CaptureCurrentValues();
        _knobFrom = _currentKnobTranslation;
        _knobTo = targetTranslation;
        _offOpacityFrom = _currentOffOpacity;
        _offOpacityTo = targetOffOpacity;
        _onOpacityFrom = _currentOnOpacity;
        _onOpacityTo = targetOnOpacity;
        _animationStarted = Stopwatch.GetTimestamp();
        _isAnimating = true;
        bool animated = WinUiCompositorMotion.AnimateTranslation(
            _knob,
            new Vector3((float)_knobFrom, 0, 0),
            new Vector3((float)_knobTo, 0, 0),
            KnobDuration,
            KnobEasing);
        animated &= WinUiCompositorMotion.AnimateOpacity(
            _iconsPanelOff,
            (float)_offOpacityFrom,
            (float)_offOpacityTo,
            IconDuration,
            new LinearEasing());
        animated &= WinUiCompositorMotion.AnimateOpacity(
            _iconsPanelOn,
            (float)_onOpacityFrom,
            (float)_onOpacityTo,
            IconDuration,
            new LinearEasing());
        if (!animated)
        {
            ApplySteadyState(isOn);
            return;
        }

        _currentKnobTranslation = _knobTo;
        _currentOffOpacity = _offOpacityTo;
        _currentOnOpacity = _onOpacityTo;
    }

    private void CaptureCurrentValues()
    {
        if (!_isAnimating)
        {
            return;
        }

        double elapsedMilliseconds = Stopwatch.GetElapsedTime(_animationStarted).TotalMilliseconds;
        double knobProgress = Math.Clamp(elapsedMilliseconds / KnobDuration.TotalMilliseconds, 0, 1);
        double iconProgress = Math.Clamp(elapsedMilliseconds / IconDuration.TotalMilliseconds, 0, 1);
        _currentKnobTranslation = Lerp(_knobFrom, _knobTo, KnobEasing.Ease(knobProgress));
        _currentOffOpacity = Lerp(_offOpacityFrom, _offOpacityTo, iconProgress);
        _currentOnOpacity = Lerp(_onOpacityFrom, _onOpacityTo, iconProgress);
        _isAnimating = knobProgress < 1 || iconProgress < 1;
    }

    private void ApplySteadyState(bool isOn)
    {
        _isAnimating = false;
        _currentKnobTranslation = isOn ? OnTranslation : 0;
        _currentOffOpacity = isOn ? 0 : 1;
        _currentOnOpacity = isOn ? 1 : 0;

        if (_iconsPanelOff is not null)
        {
            _iconsPanelOff.Opacity = _currentOffOpacity;
        }

        if (_iconsPanelOn is not null)
        {
            _iconsPanelOn.Opacity = _currentOnOpacity;
        }

        if (_knob is not null)
        {
            WinUiCompositorMotion.SetTranslation(
                _knob,
                new Vector3((float)_currentKnobTranslation, 0, 0));
        }

        if (_iconsPanelOff is not null)
        {
            WinUiCompositorMotion.SetOpacity(_iconsPanelOff, isOn ? 0 : 1);
        }

        if (_iconsPanelOn is not null)
        {
            WinUiCompositorMotion.SetOpacity(_iconsPanelOn, isOn ? 1 : 0);
        }
    }

    private static double Lerp(double start, double end, double progress)
    {
        return start + ((end - start) * progress);
    }
}
