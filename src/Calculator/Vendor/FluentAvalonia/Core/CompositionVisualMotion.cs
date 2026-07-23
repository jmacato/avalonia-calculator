using System.Numerics;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;

namespace FluentAvalonia.Core;

/// <summary>
/// Runs visual-only WinUI motion on Avalonia's composition thread.
/// </summary>
public static class CompositionVisualMotion
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
        Easing easing,
        TimeSpan delay = default)
    {
        CompositionVisual? visual = GetVisual(target);
        if (visual is null)
        {
            return false;
        }

        ExpressionAnimation centerPointAnimation =
            visual.Compositor.CreateExpressionAnimation(
                "Vector3(this.Target.Size.X * 0.5, this.Target.Size.Y * 0.5, 0)");
        visual.StartAnimation("CenterPoint", centerPointAnimation);
        visual.Scale = to;
        Vector3KeyFrameAnimation animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.Target = "Scale";
        animation.Duration = duration;
        animation.DelayTime = delay;
        animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        animation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
        visual.StartAnimation("Scale", animation);
        return true;
    }

    public static bool AnimateImplicitTranslationAndOpacity(
        Visual target,
        Vector3 translation,
        float opacity,
        TimeSpan duration,
        Easing easing,
        TimeSpan delay = default)
    {
        CompositionVisual? visual = GetVisual(target);
        if (visual is null)
        {
            return false;
        }

        var translationAnimation = visual.Compositor.CreateVector3KeyFrameAnimation();
        translationAnimation.Target = "Translation";
        translationAnimation.Duration = duration;
        translationAnimation.DelayTime = delay;
        translationAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueAfterDelay;
        translationAnimation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        translationAnimation.InsertExpressionKeyFrame(1, "this.FinalValue", easing);

        var opacityAnimation = visual.Compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.Target = "Opacity";
        opacityAnimation.Duration = duration;
        opacityAnimation.DelayTime = delay;
        opacityAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueAfterDelay;
        opacityAnimation.StopBehavior = AnimationStopBehavior.SetToFinalValue;
        opacityAnimation.InsertExpressionKeyFrame(1, "this.FinalValue", easing);

        ImplicitAnimationCollection implicitAnimations =
            visual.Compositor.CreateImplicitAnimationCollection();
        implicitAnimations["Translation"] = translationAnimation;
        implicitAnimations["Opacity"] = opacityAnimation;
        visual.ImplicitAnimations = implicitAnimations;
        visual.Translation = translation;
        visual.Opacity = opacity;
        return true;
    }

    public static void ClearImplicitAnimations(Visual target)
    {
        if (GetVisual(target) is { } visual)
        {
            visual.ImplicitAnimations = null;
        }
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
