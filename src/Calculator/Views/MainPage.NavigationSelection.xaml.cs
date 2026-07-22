using System.Numerics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.VisualTree;
using CalculatorApp.Controls;
using CalculatorApp.ViewModel.Common;
using FluentAvalonia.Core;

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
    private Border? _activeSelectionIndicator;
    private Border? _outgoingSelectionIndicator;
    private Border? _incomingSelectionIndicator;

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

        ResetPendingNavigationIndicators();
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
        double delta = nextPosition.Y - previousPosition.Y;
        double dimension = Math.Max(nextIndicator.Bounds.Height, 0.001);
        bool sameDepth = Math.Abs(nextPosition.X - previousPosition.X) <= 0.001;

        previousIndicator.Opacity = 1;
        nextIndicator.Opacity = 1;
        bool animated = sameDepth
            ? AnimateSameDepthIndicators(previousIndicator, nextIndicator, delta, dimension)
            : AnimateDifferentDepthIndicators(previousIndicator, nextIndicator, delta, dimension);
        if (!animated)
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

    private static bool AnimateSameDepthIndicators(
        Border outgoing,
        Border incoming,
        double delta,
        double dimension)
    {
        const float phaseBoundary = 0.333f;
        float peakScale = (float)(Math.Abs(delta) / dimension + 1);
        bool movingDown = delta > 0;
        float outgoingMiddleOffset = movingDown ? 0 : (float)delta;
        float incomingMiddleOffset = movingDown ? (float)-delta : 0;
        CompositionVisual? outgoingVisual = WinUiCompositorMotion.GetVisual(outgoing);
        CompositionVisual? incomingVisual = WinUiCompositorMotion.GetVisual(incoming);
        if (outgoingVisual is null || incomingVisual is null)
        {
            return false;
        }

        StartVectorAnimation(
            outgoingVisual,
            "Scale",
            new Vector3(1, 1, 1),
            new Vector3(1, peakScale, 1),
            new Vector3(1, 1, 1),
            phaseBoundary);
        StartVectorAnimation(
            outgoingVisual,
            "Translation",
            Vector3.Zero,
            new Vector3(0, outgoingMiddleOffset, 0),
            new Vector3(0, (float)delta, 0),
            phaseBoundary);
        StartVectorAnimation(
            incomingVisual,
            "Scale",
            new Vector3(1, 1, 1),
            new Vector3(1, peakScale, 1),
            new Vector3(1, 1, 1),
            phaseBoundary);
        StartVectorAnimation(
            incomingVisual,
            "Translation",
            new Vector3(0, (float)-delta, 0),
            new Vector3(0, incomingMiddleOffset, 0),
            Vector3.Zero,
            phaseBoundary);
        StartOutgoingOpacityAnimation(outgoingVisual, phaseBoundary);
        return true;
    }

    private static bool AnimateDifferentDepthIndicators(
        Border outgoing,
        Border incoming,
        double delta,
        double dimension)
    {
        CompositionVisual? outgoingVisual = WinUiCompositorMotion.GetVisual(outgoing);
        CompositionVisual? incomingVisual = WinUiCompositorMotion.GetVisual(incoming);
        if (outgoingVisual is null || incomingVisual is null)
        {
            return false;
        }

        bool nextIsBelow = delta > 0;
        float outgoingOffset = nextIsBelow ? (float)dimension : 0;
        float incomingOffset = nextIsBelow ? 0 : (float)dimension;
        StartSimpleVectorAnimation(outgoingVisual, "Scale", Vector3.One, new Vector3(1, 0, 1));
        StartSimpleVectorAnimation(outgoingVisual, "Translation", Vector3.Zero, new Vector3(0, outgoingOffset, 0));
        StartSimpleVectorAnimation(incomingVisual, "Scale", new Vector3(1, 0, 1), Vector3.One);
        StartSimpleVectorAnimation(incomingVisual, "Translation", new Vector3(0, incomingOffset, 0), Vector3.Zero);
        return true;
    }

    private static void StartVectorAnimation(
        CompositionVisual visual,
        string property,
        Vector3 from,
        Vector3 middle,
        Vector3 to,
        float phaseBoundary)
    {
        Vector3KeyFrameAnimation animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.Target = property;
        animation.Duration = NavigationSelectionDuration;
        animation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(phaseBoundary, middle, NavigationSelectionStretchEasing);
        animation.InsertKeyFrame(1, to, NavigationSelectionContractEasing);
        visual.StartAnimation(property, animation);
    }

    private static void StartSimpleVectorAnimation(
        CompositionVisual visual,
        string property,
        Vector3 from,
        Vector3 to)
    {
        Vector3KeyFrameAnimation animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.Target = property;
        animation.Duration = NavigationSelectionDuration;
        animation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, NavigationSelectionContractEasing);
        visual.StartAnimation(property, animation);
    }

    private static void StartOutgoingOpacityAnimation(CompositionVisual visual, float phaseBoundary)
    {
        ScalarKeyFrameAnimation animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.Target = "Opacity";
        animation.Duration = NavigationSelectionDuration;
        animation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        animation.InsertKeyFrame(0, 1);
        animation.InsertKeyFrame(phaseBoundary, 1);
        animation.InsertKeyFrame(1, 0, NavigationSelectionContractEasing);
        visual.StartAnimation("Opacity", animation);
    }

    private void ResetPendingNavigationIndicators()
    {
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
    }

    private void DisposeNavigationSelectionAnimation()
    {
        ResetPendingNavigationIndicators();
        _activeSelectionIndicator = null;
    }

    private static void ResetNavigationIndicator(Border indicator)
    {
        WinUiCompositorMotion.SetScale(indicator, Vector3.One);
        WinUiCompositorMotion.SetTranslation(indicator, Vector3.Zero);
        WinUiCompositorMotion.SetOpacity(indicator, (float)indicator.Opacity);
        indicator.ClearValue(Visual.RenderTransformProperty);
        indicator.ClearValue(Visual.OpacityProperty);
    }
}
