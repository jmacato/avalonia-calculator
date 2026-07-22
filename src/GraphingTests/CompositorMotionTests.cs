using System.Numerics;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
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

            Assert.True(WinUiCompositorMotion.AnimateTranslation(
                target,
                Vector3.Zero,
                new Vector3(24, 12, 0),
                TimeSpan.FromMilliseconds(100),
                new LinearEasing()));
            Assert.True(WinUiCompositorMotion.AnimateScale(
                target,
                new Vector3(0.92f, 0.92f, 1),
                Vector3.One,
                TimeSpan.FromMilliseconds(100),
                new LinearEasing()));
            Assert.True(WinUiCompositorMotion.AnimateOpacity(
                target,
                0,
                0.75f,
                TimeSpan.FromMilliseconds(100),
                new LinearEasing()));

            CompositionVisual visual = Assert.IsAssignableFrom<CompositionVisual>(
                WinUiCompositorMotion.GetVisual(target));
            Assert.Equal(new Vector3(24, 12, 0), visual.Translation);
            Assert.Equal(Vector3.One, visual.Scale);
            Assert.Equal(0.75f, visual.Opacity);
        }
        finally
        {
            window.Close();
        }
    }
}
