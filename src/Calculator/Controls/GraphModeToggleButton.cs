// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using FluentAvalonia.Core;
using GraphControl;

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
    private readonly AnimationFrameTimer _animationTimer;
    private TranslateTransform? _knobTranslation;
    private Panel? _iconsPanelOff;
    private Panel? _iconsPanelOn;
    private long _animationStarted;
    private double _knobFrom;
    private double _knobTo;
    private double _offOpacityFrom;
    private double _offOpacityTo;
    private double _onOpacityFrom;
    private double _onOpacityTo;

    public GraphModeToggleButton()
    {
        _animationTimer = new AnimationFrameTimer(OnAnimationFrame);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnApplyTemplate(e);
        _animationTimer.Stop();
        Border? knob = e.NameScope.Find<Border>("SwitchKnob");
        _iconsPanelOff = e.NameScope.Find<Panel>("IconsPanelOff");
        _iconsPanelOn = e.NameScope.Find<Panel>("IconsPanelOn");
        if (knob is null || _iconsPanelOff is null || _iconsPanelOn is null)
        {
            _knobTranslation = null;
            return;
        }

        _knobTranslation = new TranslateTransform();
        knob.RenderTransform = _knobTranslation;
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

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animationTimer.Detach();
        base.OnDetachedFromVisualTree(e);
    }

    private void AnimateToggle(bool isOn)
    {
        if (_knobTranslation is null || _iconsPanelOff is null || _iconsPanelOn is null)
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

        _knobFrom = _knobTranslation.X;
        _knobTo = targetTranslation;
        _offOpacityFrom = _iconsPanelOff.Opacity;
        _offOpacityTo = targetOffOpacity;
        _onOpacityFrom = _iconsPanelOn.Opacity;
        _onOpacityTo = targetOnOpacity;
        _animationStarted = Stopwatch.GetTimestamp();
        if (!_animationTimer.Start(this))
        {
            ApplySteadyState(isOn);
        }
    }

    private void OnAnimationFrame(TimeSpan timestamp)
    {
        _ = timestamp;
        if (_knobTranslation is null || _iconsPanelOff is null || _iconsPanelOn is null)
        {
            _animationTimer.Stop();
            return;
        }

        double elapsedMilliseconds = Stopwatch.GetElapsedTime(_animationStarted).TotalMilliseconds;
        double knobProgress = Math.Clamp(elapsedMilliseconds / KnobDuration.TotalMilliseconds, 0, 1);
        double iconProgress = Math.Clamp(elapsedMilliseconds / IconDuration.TotalMilliseconds, 0, 1);
        _knobTranslation.X = Lerp(_knobFrom, _knobTo, KnobEasing.Ease(knobProgress));
        _iconsPanelOff.Opacity = Lerp(_offOpacityFrom, _offOpacityTo, iconProgress);
        _iconsPanelOn.Opacity = Lerp(_onOpacityFrom, _onOpacityTo, iconProgress);
        if (knobProgress >= 1 && iconProgress >= 1)
        {
            _animationTimer.Stop();
            _knobTranslation.X = _knobTo;
            _iconsPanelOff.Opacity = _offOpacityTo;
            _iconsPanelOn.Opacity = _onOpacityTo;
        }
    }

    private void ApplySteadyState(bool isOn)
    {
        _animationTimer.Stop();
        if (_knobTranslation is not null)
        {
            _knobTranslation.X = isOn ? OnTranslation : 0;
        }

        if (_iconsPanelOff is not null)
        {
            _iconsPanelOff.Opacity = isOn ? 0 : 1;
        }

        if (_iconsPanelOn is not null)
        {
            _iconsPanelOn.Opacity = isOn ? 1 : 0;
        }
    }

    private static double Lerp(double start, double end, double progress)
    {
        return start + ((end - start) * progress);
    }
}
