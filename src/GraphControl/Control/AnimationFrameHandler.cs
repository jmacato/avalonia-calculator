using Avalonia.Media;
using Avalonia.Rendering.Composition;

namespace GraphControl;

internal sealed class AnimationFrameHandler(AnimationFrameTimer owner, int generation) : CompositionCustomVisualHandler
{
    private AnimationFrameTimer? _owner = owner;
    private bool _isRunning;

    public override void OnMessage(object message)
    {
        if (AnimationFrameTimer.IsStartMessage(message))
        {
            _isRunning = true;
            RegisterForNextAnimationFrameUpdate();
        }
        else if (AnimationFrameTimer.IsStopMessage(message))
        {
            _isRunning = false;
        }
        else if (AnimationFrameTimer.IsReleaseMessage(message))
        {
            _isRunning = false;
            _owner = null;
        }
    }

    public override void OnAnimationFrameUpdate()
    {
        if (!_isRunning || _owner is not { } currentOwner)
        {
            return;
        }

        currentOwner.PublishFrame(CompositionNow, generation);
        RegisterForNextAnimationFrameUpdate();
    }

    public override void OnRender(ImmediateDrawingContext drawingContext)
    {
    }
}
