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
/// Uses the Windows 11 SplitView overlay motion instead of Avalonia's width
/// interpolation. The values are from Microsoft.UI.Xaml 2.8.7's
/// SplitView_themeresources.xaml.
/// </summary>
public sealed class WinUiSplitView : SplitView
{
    private static readonly TimeSpan OpenOverlayDuration = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan CloseOverlayDuration = TimeSpan.FromMilliseconds(120);
    private static readonly SplineEasing OverlayEasing = new(0.1, 0.9, 0.2, 1.0);
    private readonly AnimationFrameTimer _animationTimer;
    private Panel? _paneRoot;
    private TranslateTransform? _paneTransform;
    private TimeSpan _animationStartedAt;
    private TimeSpan _animationDuration;
    private bool _hasAnimationStartTimestamp;
    private double _animationStart;
    private double _animationTarget;
    private double _currentTranslation;

    public WinUiSplitView()
    {
        _animationTimer = new AnimationFrameTimer(OnAnimationFrame);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnApplyTemplate(e);
        _animationTimer.Stop();
        _paneRoot = e.NameScope.Find<Panel>("PART_PaneRoot");
        if (_paneRoot is null)
        {
            _paneTransform = null;
            return;
        }

        // Avalonia's stock template animates Width. WinUI keeps the overlay at
        // OpenPaneLength and translates both the pane and its inverse clip.
        // ClipToBounds on the top-level window supplies the same visible result.
        _paneRoot.Transitions = null;
        _paneRoot.Width = OpenPaneLength;
        _paneTransform = new TranslateTransform();
        _paneRoot.RenderTransform = _paneTransform;
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

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _animationTimer.Detach();
        base.OnDetachedFromVisualTree(e);
    }

    private double ClosedTranslation => PanePlacement == SplitViewPanePlacement.Left
        ? -OpenPaneLength
        : OpenPaneLength;

    private void AnimatePane(bool isOpen)
    {
        if (_paneRoot is not { } paneRoot || _paneTransform is null)
        {
            return;
        }

        paneRoot.Width = OpenPaneLength;
        if (isOpen)
        {
            paneRoot.IsHitTestVisible = true;
        }

        double target = isOpen ? 0 : ClosedTranslation;
        if (!FAUISettings.AreAnimationsEnabled() || Math.Abs(target - _currentTranslation) <= double.Epsilon)
        {
            _animationTimer.Stop();
            ApplyTranslation(target);
            return;
        }

        _animationStart = _currentTranslation;
        _animationTarget = target;
        _animationDuration = isOpen ? OpenOverlayDuration : CloseOverlayDuration;
        _hasAnimationStartTimestamp = false;
        if (!_animationTimer.Start(this))
        {
            ApplyTranslation(target);
        }
    }

    private void OnAnimationFrame(TimeSpan timestamp)
    {
        if (!_hasAnimationStartTimestamp)
        {
            _animationStartedAt = timestamp;
            _hasAnimationStartTimestamp = true;
        }

        double progress = Math.Clamp(
            (timestamp - _animationStartedAt).TotalMilliseconds / _animationDuration.TotalMilliseconds,
            0,
            1);
        double eased = OverlayEasing.Ease(progress);
        ApplyTranslation(_animationStart + ((_animationTarget - _animationStart) * eased));
        if (progress < 1)
        {
            return;
        }

        _animationTimer.Stop();
        ApplyTranslation(_animationTarget);
    }

    private void ApplyTranslation(double translation)
    {
        _currentTranslation = translation;
        if (_paneTransform is not { } transform || _paneRoot is not { } paneRoot)
        {
            return;
        }

        transform.X = translation;
        bool isClosed = Math.Abs(translation - ClosedTranslation) <= 0.001;
        paneRoot.IsHitTestVisible = !isClosed;
    }
}
