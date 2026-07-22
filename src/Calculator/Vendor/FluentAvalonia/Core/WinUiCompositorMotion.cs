using System.Numerics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;

namespace FluentAvalonia.Core;

/// <summary>
/// Runs visual-only WinUI motion on Avalonia's composition thread.
/// </summary>
public static class WinUiCompositorMotion
{
    public static CompositionVisual? GetVisual(Visual target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return ElementComposition.GetElementVisual(target);
    }

    public static bool AnimateOpacity(
        Visual target,
        float from,
        float to,
        TimeSpan duration,
        Easing easing,
        TimeSpan delay = default)
    {
        CompositionVisual? visual = GetVisual(target);
        if (visual is null)
        {
            return false;
        }

        visual.Opacity = to;
        ScalarKeyFrameAnimation animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.Target = "Opacity";
        animation.Duration = duration;
        animation.DelayTime = delay;
        animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        animation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
        visual.StartAnimation("Opacity", animation);
        return true;
    }

    public static bool AnimateTranslation(
        Visual target,
        Vector3 from,
        Vector3 to,
        TimeSpan duration,
        Easing easing,
        TimeSpan delay = default)
    {
        CompositionVisual? visual = GetVisual(target);
        if (visual is null)
        {
            return false;
        }

        visual.Translation = to;
        Vector3KeyFrameAnimation animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.Target = "Translation";
        animation.Duration = duration;
        animation.DelayTime = delay;
        animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        animation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
        visual.StartAnimation("Translation", animation);
        return true;
    }

    public static bool AnimateScale(
        Visual target,
        Vector3 from,
        Vector3 to,
        TimeSpan duration,
        Easing easing)
    {
        CompositionVisual? visual = GetVisual(target);
        if (visual is null)
        {
            return false;
        }

        visual.CenterPoint = new Vector3(
            (float)(target.Bounds.Width * 0.5),
            (float)(target.Bounds.Height * 0.5),
            0);
        visual.Scale = to;
        Vector3KeyFrameAnimation animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.Target = "Scale";
        animation.Duration = duration;
        animation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
        visual.StartAnimation("Scale", animation);
        return true;
    }

    public static void SetOpacity(Visual target, float opacity)
    {
        if (GetVisual(target) is { } visual)
        {
            visual.StopAnimation("Opacity");
            visual.Opacity = opacity;
        }
    }

    public static void SetTranslation(Visual target, Vector3 translation)
    {
        if (GetVisual(target) is { } visual)
        {
            visual.StopAnimation("Translation");
            visual.Translation = translation;
        }
    }

    public static void SetScale(Visual target, Vector3 scale)
    {
        if (GetVisual(target) is { } visual)
        {
            visual.StopAnimation("Scale");
            visual.Scale = scale;
        }
    }
}
