using System.Numerics;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using CalculatorApp.Controls;
using FluentAvalonia.Core;

namespace GraphingTests;

public sealed class CompositorMotionTests
{
    [AvaloniaFact(Timeout = 5_000)]
    public void VisualMotionPublishesFinalValuesWithoutUiFrameCallbacks()
    {
        var target = new Border
        {
            Width = 120,
            Height = 80
        };
        var window = new Window
        {
            Width = 240,
            Height = 160,
            Content = target
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.True(CompositionVisualMotion.AnimateTranslation(
                target,
                Vector3.Zero,
                new Vector3(24, 12, 0),
                TimeSpan.FromMilliseconds(100),
                new LinearEasing()));
            Assert.True(CompositionVisualMotion.AnimateScale(
                target,
                new Vector3(0.92f, 0.92f, 1),
                Vector3.One,
                TimeSpan.FromMilliseconds(100),
                new LinearEasing()));
            Assert.True(CompositionVisualMotion.AnimateOpacity(
                target,
                0,
                0.75f,
                TimeSpan.FromMilliseconds(100),
                new LinearEasing()));

            CompositionVisual visual = Assert.IsAssignableFrom<CompositionVisual>(
                CompositionVisualMotion.GetVisual(target));
            Assert.Equal(new Vector3(24, 12, 0), visual.Translation);
            Assert.Equal(Vector3.One, visual.Scale);
            Assert.Equal(0.75f, visual.Opacity);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact(Timeout = 5_000)]
    public void KeypadCompositionMotionStartsAtVisualTreeAttachment()
    {
        var target = new Border
        {
            Width = 120,
            Height = 80
        };
        var holder = new Border
        {
            IsVisible = false,
            Child = target
        };
        var window = new Window
        {
            Width = 240,
            Height = 160,
            Content = holder
        };
        var profile = new CompositionMotionProfile();
        profile.Animations.Add(
            new CompositionMotionTrack
            {
                Property = CompositionMotionProperty.Scale,
                FromX = 0.92,
                FromY = 0.92,
                FromZ = 1,
                ToX = 1,
                ToY = 1,
                ToZ = 1,
                Duration = TimeSpan.FromMilliseconds(100)
            });
        CompositionMotionBehavior.SetProfile(target, profile);

        try
        {
            window.Show();

            CompositionMotionState state =
                Assert.IsType<CompositionMotionState>(
                    CompositionMotionBehavior.GetState(target));
            Assert.Equal(0, state.StartCount);
            CompositionVisual visual = Assert.IsAssignableFrom<CompositionVisual>(
                CompositionVisualMotion.GetVisual(target));
            Assert.Equal(new Vector3(0.92f, 0.92f, 1), visual.Scale);

            holder.IsVisible = true;

            Assert.Equal(1, state.StartCount);
            Assert.Equal(Vector3.One, visual.Scale);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact(Timeout = 5_000)]
    public async Task ThemeTransitionCompositionGatePrecedesHostRealization()
    {
        var child = new Border
        {
            Width = 80,
            Height = 40
        };
        var panel = new StackPanel
        {
            Children = { child }
        };
        var host = new ThemeTransitionHost
        {
            Width = 160,
            Height = 120,
            Child = panel,
            ThemeTransitions =
            {
                new EntranceThemeTransition()
            }
        };
        ThemeTransitionBehavior.SetChildrenTransitions(
            panel,
            [
                new EntranceThemeTransition
                {
                    FromVerticalOffset = 50,
                    IsStaggeringEnabled = true
                },
                new RepositionThemeTransition
                {
                    IsStaggeringEnabled = false
                }
            ]);
        var window = new Window
        {
            Width = 240,
            Height = 160,
            Content = host
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            host.IsOpen = true;

            CompositionVisual visual = Assert.IsAssignableFrom<CompositionVisual>(
                CompositionVisualMotion.GetVisual(host));
            Assert.Equal(0, visual.Opacity);
            Assert.False(host.IsVisible);
            Assert.Equal(1, host.Opacity);
            Assert.Equal(1, child.Opacity);
            Assert.Null(host.RenderTransform);
            Assert.Null(child.RenderTransform);

            TimeSpan? duration = await host.OpeningTask.ConfigureAwait(true);
            Assert.NotNull(duration);
            Assert.True(host.IsVisible);
            Assert.NotNull(visual.ImplicitAnimations);

            host.IsOpen = false;
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }
}
