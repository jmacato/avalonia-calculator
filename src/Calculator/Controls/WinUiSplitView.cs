using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using FluentAvalonia.Core;

namespace CalculatorApp.Controls;

/// <summary>
/// Uses the Windows 11 SplitView overlay motion instead of Avalonia's width
/// interpolation. The values are from Microsoft.UI.Xaml 2.8.7's
/// SplitView_themeresources.xaml.
/// </summary>
public sealed class WinUiSplitView : SplitView
{
    private static readonly TimeSpan OpenOverlayDuration = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan CloseOverlayDuration = TimeSpan.FromMilliseconds(120);
    private static readonly SplineEasing OverlayEasing = new(0.1, 0.9, 0.2, 1.0);
    private Panel? _paneRoot;
    private long _animationStarted;
    private TimeSpan _animationDuration;
    private bool _isAnimating;
    private double _animationStart;
    private double _animationTarget;
    private double _currentTranslation;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnApplyTemplate(e);
        _isAnimating = false;
        _paneRoot = e.NameScope.Find<Panel>("PART_PaneRoot");
        if (_paneRoot is null)
        {
            return;
        }

        // Avalonia's stock template animates Width. WinUI keeps the overlay at
        // OpenPaneLength and translates both the pane and its inverse clip.
        // ClipToBounds on the top-level window supplies the same visible result.
        _paneRoot.Transitions = null;
        _paneRoot.Width = OpenPaneLength;
        ApplyTranslation(IsPaneOpen ? 0 : ClosedTranslation);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == IsPaneOpenProperty)
        {
            AnimatePane(change.GetNewValue<bool>());
        }
        else if (change.Property == OpenPaneLengthProperty && _paneRoot is { } paneRoot)
        {
            paneRoot.Width = change.GetNewValue<double>();
            if (!IsPaneOpen)
            {
                ApplyTranslation(ClosedTranslation);
            }
        }
    }

    private double ClosedTranslation => PanePlacement == SplitViewPanePlacement.Left
        ? -OpenPaneLength
        : OpenPaneLength;

    private void AnimatePane(bool isOpen)
    {
        if (_paneRoot is not { } paneRoot)
        {
            return;
        }

        paneRoot.Width = OpenPaneLength;
        if (isOpen)
        {
            paneRoot.IsHitTestVisible = true;
        }

        double target = isOpen ? 0 : ClosedTranslation;
        CaptureCurrentTranslation();
        if (!FAUISettings.AreAnimationsEnabled() || Math.Abs(target - _currentTranslation) <= double.Epsilon)
        {
            ApplyTranslation(target);
            return;
        }

        _animationStart = _currentTranslation;
        _animationTarget = target;
        _animationDuration = isOpen ? OpenOverlayDuration : CloseOverlayDuration;
        _animationStarted = Stopwatch.GetTimestamp();
        _isAnimating = true;
        paneRoot.IsHitTestVisible = isOpen;
        if (!WinUiCompositorMotion.AnimateTranslation(
                paneRoot,
                new Vector3((float)_animationStart, 0, 0),
                new Vector3((float)_animationTarget, 0, 0),
                _animationDuration,
                OverlayEasing))
        {
            ApplyTranslation(target);
            return;
        }

        _currentTranslation = target;
    }

    private void CaptureCurrentTranslation()
    {
        if (!_isAnimating)
        {
            return;
        }

        double progress = Math.Clamp(
            Stopwatch.GetElapsedTime(_animationStarted).TotalMilliseconds / _animationDuration.TotalMilliseconds,
            0,
            1);
        double eased = OverlayEasing.Ease(progress);
        _currentTranslation = _animationStart + ((_animationTarget - _animationStart) * eased);
        _isAnimating = progress < 1;
    }

    private void ApplyTranslation(double translation)
    {
        _currentTranslation = translation;
        _isAnimating = false;
        if (_paneRoot is not { } paneRoot)
        {
            return;
        }

        WinUiCompositorMotion.SetTranslation(
            paneRoot,
            new Vector3((float)translation, 0, 0));
        bool isClosed = Math.Abs(translation - ClosedTranslation) <= 0.001;
        paneRoot.IsHitTestVisible = !isClosed;
    }
}
