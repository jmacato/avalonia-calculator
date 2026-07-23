// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Numerics;
using Avalonia.Controls;
using FluentAvalonia.Core;

namespace CalculatorApp.Controls;

internal static class CompositionMotionRunner
{
    internal static TimeSpan GetDuration(CompositionMotionProfile profile)
    {
        TimeSpan duration = TimeSpan.Zero;
        foreach (CompositionMotionTrack animation in profile.Animations)
        {
            TimeSpan trackDuration = animation.Delay + animation.Duration;
            if (trackDuration > duration)
            {
                duration = trackDuration;
            }
        }

        return duration;
    }

    internal static void Apply(
        Control target,
        CompositionMotionProfile profile,
        bool useFromValues)
    {
        foreach (CompositionMotionTrack animation in profile.Animations)
        {
            Apply(target, animation, useFromValues);
        }
    }

    internal static bool Play(
        Control target,
        CompositionMotionProfile profile,
        bool forward)
    {
        Apply(target, profile, useFromValues: forward);
        bool started = false;
        foreach (CompositionMotionTrack animation in profile.Animations)
        {
            started |= Play(target, animation, forward);
        }

        return started;
    }

    private static void Apply(
        Control target,
        CompositionMotionTrack animation,
        bool useFromValue)
    {
        switch (animation.Property)
        {
            case CompositionMotionProperty.Opacity:
                CompositionVisualMotion.SetOpacity(
                    target,
                    (float)(useFromValue ? animation.From : animation.To));
                break;
            case CompositionMotionProperty.Scale:
                CompositionVisualMotion.SetScale(
                    target,
                    animation.GetVector(target, useFromValue));
                break;
            case CompositionMotionProperty.Translation:
                CompositionVisualMotion.SetTranslation(
                    target,
                    animation.GetVector(target, useFromValue));
                break;
        }
    }

    private static bool Play(
        Control target,
        CompositionMotionTrack animation,
        bool forward)
    {
        if (animation.Duration <= TimeSpan.Zero)
        {
            Apply(target, animation, useFromValue: !forward);
            return false;
        }

        var easing = animation.CreateEasing();
        switch (animation.Property)
        {
            case CompositionMotionProperty.Opacity:
                return CompositionVisualMotion.AnimateOpacity(
                    target,
                    (float)(forward ? animation.From : animation.To),
                    (float)(forward ? animation.To : animation.From),
                    animation.Duration,
                    easing,
                    animation.Delay);
            case CompositionMotionProperty.Scale:
                Vector3 scaleFrom = animation.GetVector(target, forward);
                Vector3 scaleTo = animation.GetVector(target, !forward);
                return CompositionVisualMotion.AnimateScale(
                    target,
                    scaleFrom,
                    scaleTo,
                    animation.Duration,
                    easing,
                    animation.Delay);
            case CompositionMotionProperty.Translation:
                Vector3 translationFrom = animation.GetVector(target, forward);
                Vector3 translationTo = animation.GetVector(target, !forward);
                return CompositionVisualMotion.AnimateTranslation(
                    target,
                    translationFrom,
                    translationTo,
                    animation.Duration,
                    easing,
                    animation.Delay);
            default:
                return false;
        }
    }
}
