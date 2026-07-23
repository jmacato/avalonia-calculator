// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Numerics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.Core;

namespace CalculatorApp.Controls;

internal sealed class NavigationSelectionCoordinator
{
    private readonly ItemsControl _owner;
    private Border? _activeIndicator;
    private Button? _pendingButton;
    private NavigationSelectionTransitionSettings? _pendingSettings;
    private Border? _outgoingIndicator;
    private Border? _incomingIndicator;
    private bool _selectionQueued;

    internal NavigationSelectionCoordinator(ItemsControl owner)
    {
        _owner = owner;
    }

    internal void Select(
        Button button,
        NavigationSelectionTransitionSettings settings)
    {
        _pendingButton = button;
        _pendingSettings = settings;
        if (FindIndicator(button, settings.IndicatorName) is { } indicator)
        {
            CompositionVisualMotion.SetOpacity(indicator, 0);
        }

        if (_selectionQueued)
        {
            return;
        }

        _selectionQueued = true;
        Dispatcher.UIThread.Post(CompletePendingSelection, DispatcherPriority.Render);
    }

    internal void Remove(Button button)
    {
        if (ReferenceEquals(_pendingButton, button))
        {
            _pendingButton = null;
            _pendingSettings = null;
        }

        if (_activeIndicator is not null &&
            _activeIndicator.GetVisualAncestors().Contains(button))
        {
            ResetPendingIndicators();
            _activeIndicator = null;
        }
    }

    private void CompletePendingSelection()
    {
        _selectionQueued = false;
        Button? button = _pendingButton;
        NavigationSelectionTransitionSettings? settings = _pendingSettings;
        _pendingButton = null;
        _pendingSettings = null;
        if (button is null || settings is null)
        {
            return;
        }

        Border? indicator = FindIndicator(button, settings.IndicatorName);
        if (indicator is not null)
        {
            AnimateTo(indicator, settings);
        }
    }

    private void AnimateTo(
        Border nextIndicator,
        NavigationSelectionTransitionSettings settings)
    {
        Border? previousIndicator = _activeIndicator;
        if (previousIndicator is null ||
            ReferenceEquals(previousIndicator, nextIndicator))
        {
            _activeIndicator = nextIndicator;
            ResetIndicator(nextIndicator);
            return;
        }

        ResetPendingIndicators();
        _activeIndicator = nextIndicator;
        if (!FAUISettings.AreAnimationsEnabled() ||
            previousIndicator.TranslatePoint(default, _owner) is not { } previousPosition ||
            nextIndicator.TranslatePoint(default, _owner) is not { } nextPosition)
        {
            ResetIndicator(previousIndicator);
            ResetIndicator(nextIndicator);
            return;
        }

        _outgoingIndicator = previousIndicator;
        _incomingIndicator = nextIndicator;
        double delta = nextPosition.Y - previousPosition.Y;
        double dimension = Math.Max(nextIndicator.Bounds.Height, 0.001);
        bool sameDepth =
            Math.Abs(nextPosition.X - previousPosition.X) <= 0.001;

        previousIndicator.Opacity = 1;
        nextIndicator.Opacity = 1;
        bool animated = sameDepth
            ? AnimateSameDepth(
                previousIndicator,
                nextIndicator,
                delta,
                dimension,
                settings)
            : AnimateDifferentDepth(
                previousIndicator,
                nextIndicator,
                delta,
                dimension,
                settings);
        if (!animated)
        {
            ResetIndicator(previousIndicator);
            ResetIndicator(nextIndicator);
            _outgoingIndicator = null;
            _incomingIndicator = null;
        }
    }

    private static Border? FindIndicator(Button button, string indicatorName) =>
        button.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(border => border.Name == indicatorName);

    private static bool AnimateSameDepth(
        Border outgoing,
        Border incoming,
        double delta,
        double dimension,
        NavigationSelectionTransitionSettings settings)
    {
        float phaseBoundary = (float)Math.Clamp(
            settings.PhaseBoundary,
            0.001,
            0.999);
        float peakScale = (float)(Math.Abs(delta) / dimension + 1);
        bool movingDown = delta > 0;
        float outgoingMiddleOffset = movingDown ? 0 : (float)delta;
        float incomingMiddleOffset = movingDown ? (float)-delta : 0;
        CompositionVisual? outgoingVisual =
            CompositionVisualMotion.GetVisual(outgoing);
        CompositionVisual? incomingVisual =
            CompositionVisualMotion.GetVisual(incoming);
        if (outgoingVisual is null || incomingVisual is null)
        {
            return false;
        }

        StartVectorAnimation(
            outgoingVisual,
            "Scale",
            Vector3.One,
            new Vector3(1, peakScale, 1),
            Vector3.One,
            phaseBoundary,
            settings);
        StartVectorAnimation(
            outgoingVisual,
            "Translation",
            Vector3.Zero,
            new Vector3(0, outgoingMiddleOffset, 0),
            new Vector3(0, (float)delta, 0),
            phaseBoundary,
            settings);
        StartVectorAnimation(
            incomingVisual,
            "Scale",
            Vector3.One,
            new Vector3(1, peakScale, 1),
            Vector3.One,
            phaseBoundary,
            settings);
        StartVectorAnimation(
            incomingVisual,
            "Translation",
            new Vector3(0, (float)-delta, 0),
            new Vector3(0, incomingMiddleOffset, 0),
            Vector3.Zero,
            phaseBoundary,
            settings);
        StartOutgoingOpacityAnimation(
            outgoingVisual,
            phaseBoundary,
            settings);
        return true;
    }

    private static bool AnimateDifferentDepth(
        Border outgoing,
        Border incoming,
        double delta,
        double dimension,
        NavigationSelectionTransitionSettings settings)
    {
        CompositionVisual? outgoingVisual =
            CompositionVisualMotion.GetVisual(outgoing);
        CompositionVisual? incomingVisual =
            CompositionVisualMotion.GetVisual(incoming);
        if (outgoingVisual is null || incomingVisual is null)
        {
            return false;
        }

        bool nextIsBelow = delta > 0;
        float outgoingOffset = nextIsBelow ? (float)dimension : 0;
        float incomingOffset = nextIsBelow ? 0 : (float)dimension;
        StartSimpleVectorAnimation(
            outgoingVisual,
            "Scale",
            Vector3.One,
            new Vector3(1, 0, 1),
            settings);
        StartSimpleVectorAnimation(
            outgoingVisual,
            "Translation",
            Vector3.Zero,
            new Vector3(0, outgoingOffset, 0),
            settings);
        StartSimpleVectorAnimation(
            incomingVisual,
            "Scale",
            new Vector3(1, 0, 1),
            Vector3.One,
            settings);
        StartSimpleVectorAnimation(
            incomingVisual,
            "Translation",
            new Vector3(0, incomingOffset, 0),
            Vector3.Zero,
            settings);
        return true;
    }

    private static void StartVectorAnimation(
        CompositionVisual visual,
        string property,
        Vector3 from,
        Vector3 middle,
        Vector3 to,
        float phaseBoundary,
        NavigationSelectionTransitionSettings settings)
    {
        Vector3KeyFrameAnimation animation =
            visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.Target = property;
        animation.Duration = settings.Duration;
        animation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(
            phaseBoundary,
            middle,
            CreateStretchEasing(settings));
        animation.InsertKeyFrame(1, to, CreateContractEasing(settings));
        visual.StartAnimation(property, animation);
    }

    private static void StartSimpleVectorAnimation(
        CompositionVisual visual,
        string property,
        Vector3 from,
        Vector3 to,
        NavigationSelectionTransitionSettings settings)
    {
        Vector3KeyFrameAnimation animation =
            visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.Target = property;
        animation.Duration = settings.Duration;
        animation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, CreateContractEasing(settings));
        visual.StartAnimation(property, animation);
    }

    private static void StartOutgoingOpacityAnimation(
        CompositionVisual visual,
        float phaseBoundary,
        NavigationSelectionTransitionSettings settings)
    {
        ScalarKeyFrameAnimation animation =
            visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.Target = "Opacity";
        animation.Duration = settings.Duration;
        animation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        animation.InsertKeyFrame(0, 1);
        animation.InsertKeyFrame(phaseBoundary, 1);
        animation.InsertKeyFrame(1, 0, CreateContractEasing(settings));
        visual.StartAnimation("Opacity", animation);
    }

    private static SplineEasing CreateStretchEasing(
        NavigationSelectionTransitionSettings settings) =>
        new(
            settings.StretchControlPoint1.X,
            settings.StretchControlPoint1.Y,
            settings.StretchControlPoint2.X,
            settings.StretchControlPoint2.Y);

    private static SplineEasing CreateContractEasing(
        NavigationSelectionTransitionSettings settings) =>
        new(
            settings.ContractControlPoint1.X,
            settings.ContractControlPoint1.Y,
            settings.ContractControlPoint2.X,
            settings.ContractControlPoint2.Y);

    private void ResetPendingIndicators()
    {
        if (_outgoingIndicator is { } outgoing)
        {
            ResetIndicator(outgoing);
        }

        if (_incomingIndicator is { } incoming)
        {
            ResetIndicator(incoming);
        }

        _outgoingIndicator = null;
        _incomingIndicator = null;
    }

    private static void ResetIndicator(Border indicator)
    {
        CompositionVisualMotion.SetScale(indicator, Vector3.One);
        CompositionVisualMotion.SetTranslation(indicator, Vector3.Zero);
        CompositionVisualMotion.SetOpacity(indicator, (float)indicator.Opacity);
        indicator.ClearValue(Visual.RenderTransformProperty);
        indicator.ClearValue(Visual.OpacityProperty);
    }
}
