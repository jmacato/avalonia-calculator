using Avalonia;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;

namespace GraphControl;

/// <summary>
/// Repeats a UI-thread callback at the owning visual's composition-frame cadence.
/// </summary>
public sealed class AnimationFrameTimer
{
    private static readonly object StartMessage = new();
    private static readonly object StopMessage = new();
    private static readonly object ReleaseMessage = new();
    private readonly Action<TimeSpan> _tick;
    private readonly Action _dispatchFrameCallback;
    private Visual? _owner;
    private CompositionCustomVisual? _customVisual;
    private long _publishedTimestampTicks;
    private long _publishedSequence;
    private long _dispatchedSequence;
    private int _publishedGeneration;
    private int _generation;
    private int _dispatchPending;
    private int _isRunning;

    public AnimationFrameTimer(Action<TimeSpan> tick)
    {
        ArgumentNullException.ThrowIfNull(tick);
        _tick = tick;
        _dispatchFrameCallback = DispatchFrame;
    }

    public bool IsRunning => Volatile.Read(ref _isRunning) != 0;

    public bool Start(Visual owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Dispatcher.UIThread.VerifyAccess();

        if (ReferenceEquals(_owner, owner) && _customVisual is { } retainedVisual)
        {
            if (Interlocked.Exchange(ref _isRunning, 1) == 0)
            {
                retainedVisual.SendHandlerMessage(StartMessage);
            }

            return true;
        }

        Detach();
        CompositionVisual? elementVisual = ElementComposition.GetElementVisual(owner);
        if (elementVisual is null || ElementComposition.GetElementChildVisual(owner) is not null)
        {
            return false;
        }

        int generation = unchecked(++_generation);
        var handler = new AnimationFrameHandler(this, generation);
        CompositionCustomVisual customVisual = elementVisual.Compositor.CreateCustomVisual(handler);
        ElementComposition.SetElementChildVisual(owner, customVisual);
        _owner = owner;
        _customVisual = customVisual;
        Volatile.Write(ref _isRunning, 1);
        customVisual.SendHandlerMessage(StartMessage);
        return true;
    }

    public void Stop()
    {
        Dispatcher.UIThread.VerifyAccess();
        Volatile.Write(ref _isRunning, 0);
        _customVisual?.SendHandlerMessage(StopMessage);
    }

    public void Detach()
    {
        Dispatcher.UIThread.VerifyAccess();
        Volatile.Write(ref _isRunning, 0);
        unchecked
        {
            _generation++;
        }

        if (_customVisual is { } customVisual)
        {
            customVisual.SendHandlerMessage(ReleaseMessage);
            if (_owner is { } owner && ReferenceEquals(ElementComposition.GetElementChildVisual(owner), customVisual))
            {
                ElementComposition.SetElementChildVisual(owner, null);
            }
        }

        _customVisual = null;
        _owner = null;
    }

    internal static bool IsStartMessage(object message) => ReferenceEquals(message, StartMessage);
    internal static bool IsStopMessage(object message) => ReferenceEquals(message, StopMessage);
    internal static bool IsReleaseMessage(object message) => ReferenceEquals(message, ReleaseMessage);

    internal void PublishFrame(TimeSpan timestamp, int generation)
    {
        Volatile.Write(ref _publishedTimestampTicks, timestamp.Ticks);
        Volatile.Write(ref _publishedGeneration, generation);
        _ = Interlocked.Increment(ref _publishedSequence);
        if (Interlocked.Exchange(ref _dispatchPending, 1) == 0)
        {
            Dispatcher.UIThread.Post(_dispatchFrameCallback, DispatcherPriority.Render);
        }
    }

    private void DispatchFrame()
    {
        Dispatcher.UIThread.VerifyAccess();
        _ = Interlocked.Exchange(ref _dispatchPending, 0);
        long sequence = Volatile.Read(ref _publishedSequence);
        if (sequence == _dispatchedSequence)
        {
            return;
        }

        _dispatchedSequence = sequence;
        if (Volatile.Read(ref _isRunning) == 0 || Volatile.Read(ref _publishedGeneration) != _generation)
        {
            return;
        }

        _tick(TimeSpan.FromTicks(Volatile.Read(ref _publishedTimestampTicks)));
    }
}
