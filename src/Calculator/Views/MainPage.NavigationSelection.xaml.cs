using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using CalculatorApp.ViewModel.Common;
using FluentAvalonia.Core;
using GraphControl;

namespace CalculatorApp;

public sealed partial class MainPage
{
    // Microsoft.UI.Xaml.Controls.NavigationView.cpp, v2.8.7:
    //   c_frame1point1=(0.9,0.1), c_frame1point2=(1.0,0.2)
    //   c_frame2point1=(0.1,0.9), c_frame2point2=(0.2,1.0)
    //   indicator duration=600 ms, center-point step duration=200 ms.
    private static readonly TimeSpan NavigationSelectionDuration = TimeSpan.FromMilliseconds(600);
    private static readonly SplineEasing NavigationSelectionStretchEasing = new(0.9, 0.1, 1.0, 0.2);
    private static readonly SplineEasing NavigationSelectionContractEasing = new(0.1, 0.9, 0.2, 1.0);
    private readonly AnimationFrameTimer _navigationSelectionAnimationTimer;
    private Border? _activeSelectionIndicator;
    private Border? _outgoingSelectionIndicator;
    private Border? _incomingSelectionIndicator;
    private TimeSpan _navigationSelectionStartedAt;
    private bool _hasNavigationSelectionStartTimestamp;
    private double _navigationSelectionDelta;
    private double _navigationSelectionDimension;
    private bool _navigationSelectionSameDepth;

    private void CaptureInitialNavigationSelection()
    {
        _activeSelectionIndicator = FindSelectedNavigationIndicator();
    }

    private void AnimateNavigationSelectionChange()
    {
        Border? nextIndicator = FindSelectedNavigationIndicator();
        Border? previousIndicator = _activeSelectionIndicator;
        if (nextIndicator is null)
        {
            return;
        }

        if (previousIndicator is null || ReferenceEquals(previousIndicator, nextIndicator))
        {
            _activeSelectionIndicator = nextIndicator;
            ResetNavigationIndicator(nextIndicator);
            return;
        }

        StopNavigationSelectionAnimation();
        _activeSelectionIndicator = nextIndicator;
        if (!FAUISettings.AreAnimationsEnabled()
            || previousIndicator.TranslatePoint(default, NavList) is not { } previousPosition
            || nextIndicator.TranslatePoint(default, NavList) is not { } nextPosition)
        {
            ResetNavigationIndicator(previousIndicator);
            ResetNavigationIndicator(nextIndicator);
            return;
        }

        _outgoingSelectionIndicator = previousIndicator;
        _incomingSelectionIndicator = nextIndicator;
        _navigationSelectionDelta = nextPosition.Y - previousPosition.Y;
        _navigationSelectionDimension = Math.Max(nextIndicator.Bounds.Height, 0.001);
        _navigationSelectionSameDepth = Math.Abs(nextPosition.X - previousPosition.X) <= 0.001;
        _hasNavigationSelectionStartTimestamp = false;

        previousIndicator.Opacity = 1;
        nextIndicator.Opacity = 1;
        if (!_navigationSelectionAnimationTimer.Start(this))
        {
            ResetNavigationIndicator(previousIndicator);
            ResetNavigationIndicator(nextIndicator);
            _outgoingSelectionIndicator = null;
            _incomingSelectionIndicator = null;
        }
    }

    private Border? FindSelectedNavigationIndicator()
    {
        Button? selectedButton = NavList.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => button.DataContext is NavCategory { IsSelected: true });
        return selectedButton?.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(border => border.Name == "SelectionIndicator");
    }

    private void OnNavigationSelectionAnimationFrame(TimeSpan timestamp)
    {
        if (_outgoingSelectionIndicator is not { } outgoing
            || _incomingSelectionIndicator is not { } incoming)
        {
            StopNavigationSelectionAnimation();
            return;
        }

        if (!_hasNavigationSelectionStartTimestamp)
        {
            _navigationSelectionStartedAt = timestamp;
            _hasNavigationSelectionStartTimestamp = true;
        }

        double progress = Math.Clamp(
            (timestamp - _navigationSelectionStartedAt).TotalMilliseconds / NavigationSelectionDuration.TotalMilliseconds,
            0,
            1);

        if (_navigationSelectionSameDepth)
        {
            ApplySameDepthIndicatorFrame(outgoing, 0, _navigationSelectionDelta, progress, isOutgoing: true);
            ApplySameDepthIndicatorFrame(incoming, -_navigationSelectionDelta, 0, progress, isOutgoing: false);
        }
        else
        {
            bool nextIsBelow = _navigationSelectionDelta > 0;
            ApplyDifferentDepthIndicatorFrame(outgoing, progress, isOutgoing: true, fromTop: !nextIsBelow);
            ApplyDifferentDepthIndicatorFrame(incoming, progress, isOutgoing: false, fromTop: nextIsBelow);
        }

        if (progress < 1)
        {
            return;
        }

        StopNavigationSelectionAnimation();
        ResetNavigationIndicator(outgoing);
        ResetNavigationIndicator(incoming);
        _outgoingSelectionIndicator = null;
        _incomingSelectionIndicator = null;
    }

    private void ApplySameDepthIndicatorFrame(Border indicator, double from, double to, double progress, bool isOutgoing)
    {
        const double phaseBoundary = 0.333;
        double peakScale = Math.Abs(to - from) / _navigationSelectionDimension + 1;
        double scale;
        double offset;
        double center;

        if (progress < phaseBoundary)
        {
            double phaseProgress = progress / phaseBoundary;
            scale = Lerp(1, peakScale, NavigationSelectionStretchEasing.Ease(phaseProgress));
            offset = from;
            center = from < to ? 0 : _navigationSelectionDimension;
        }
        else
        {
            double phaseProgress = (progress - phaseBoundary) / (1 - phaseBoundary);
            scale = Lerp(peakScale, 1, NavigationSelectionContractEasing.Ease(phaseProgress));
            offset = to;
            center = from < to ? _navigationSelectionDimension : 0;
        }

        // Composition's Scale.Y is evaluated around CenterPoint.Y, then Offset.Y
        // is applied. This matrix is the identical affine transform.
        double matrixOffset = offset + (center * (1 - scale));
        indicator.RenderTransform = new MatrixTransform(new Matrix(1, 0, 0, scale, 0, matrixOffset));

        if (isOutgoing)
        {
            double opacityProgress = progress < phaseBoundary
                ? 0
                : NavigationSelectionContractEasing.Ease((progress - phaseBoundary) / (1 - phaseBoundary));
            indicator.Opacity = 1 - opacityProgress;
        }
    }

    private void ApplyDifferentDepthIndicatorFrame(Border indicator, double progress, bool isOutgoing, bool fromTop)
    {
        double scale = isOutgoing ? 1 - progress : progress;
        double center = fromTop ? 0 : _navigationSelectionDimension;
        double matrixOffset = center * (1 - scale);
        indicator.RenderTransform = new MatrixTransform(new Matrix(1, 0, 0, scale, 0, matrixOffset));
    }

    private void StopNavigationSelectionAnimation()
    {
        _navigationSelectionAnimationTimer.Stop();
        _hasNavigationSelectionStartTimestamp = false;
    }

    private void DisposeNavigationSelectionAnimation()
    {
        _navigationSelectionAnimationTimer.Detach();
        if (_outgoingSelectionIndicator is { } outgoing)
        {
            ResetNavigationIndicator(outgoing);
        }

        if (_incomingSelectionIndicator is { } incoming)
        {
            ResetNavigationIndicator(incoming);
        }

        _outgoingSelectionIndicator = null;
        _incomingSelectionIndicator = null;
        _activeSelectionIndicator = null;
    }

    private static void ResetNavigationIndicator(Border indicator)
    {
        indicator.ClearValue(Visual.RenderTransformProperty);
        indicator.ClearValue(Visual.OpacityProperty);
    }

    private static double Lerp(double from, double to, double progress) => from + ((to - from) * progress);
}
