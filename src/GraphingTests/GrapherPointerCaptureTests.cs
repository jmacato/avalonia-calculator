using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using GraphControl;

namespace GraphingTests;

public sealed class GrapherPointerCaptureTests
{
    [AvaloniaFact]
    public void GraphSurfaceAcceptsKeyboardFocus()
    {
        using var grapher = new Grapher();

        Assert.True(grapher.Focusable);
    }

    [AvaloniaFact]
    public void GraphSurfaceExplicitlyEnablesAntialiasing()
    {
        using var grapher = new Grapher();

        Assert.Equal(EdgeMode.Antialias, RenderOptions.GetEdgeMode(grapher));
    }

    [AvaloniaFact]
    public void PointerManipulationCapturesUntilRelease()
    {
        using var grapher = new Grapher();
        using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);

        grapher.RaiseEvent(new PointerPressedEventArgs(
            grapher,
            pointer,
            grapher,
            new Point(100, 100),
            timestamp: 1,
            new PointerPointProperties(
                RawInputModifiers.LeftMouseButton,
                PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));

        Assert.Same(grapher, pointer.Captured);

        grapher.RaiseEvent(new PointerReleasedEventArgs(
            grapher,
            pointer,
            grapher,
            new Point(100, 100),
            timestamp: 2,
            new PointerPointProperties(
                RawInputModifiers.None,
                PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None,
            MouseButton.Left));

        Assert.Null(pointer.Captured);
    }

    [AvaloniaFact]
    public void PinchGestureDoesNotRestartPanInertiaAfterStaggeredRelease()
    {
        using var grapher = new Grapher();
        using var firstPointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        using var secondPointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
        grapher.Measure(new Size(400, 600));
        grapher.Arrange(new Rect(0, 0, 400, 600));

        RaisePressed(grapher, firstPointer, new Point(120, 240), 1);
        RaisePressed(grapher, secondPointer, new Point(280, 240), 2);
        RaiseMoved(grapher, firstPointer, new Point(100, 240), 3);
        RaiseMoved(grapher, secondPointer, new Point(300, 240), 4);

        RaiseReleased(grapher, firstPointer, new Point(100, 240), 5);

        Assert.True(grapher.IsPanInertiaSuppressedForGesture);
        Assert.False(grapher.IsPanInertiaEligible);

        RaiseMoved(grapher, secondPointer, new Point(350, 240), 6);
        RaiseReleased(grapher, secondPointer, new Point(350, 240), 7);

        Assert.False(grapher.IsPanInertiaActive);
        Assert.False(grapher.IsPanInertiaSuppressedForGesture);
    }

    private static void RaisePressed(Grapher grapher, Pointer pointer, Point point, ulong timestamp) =>
        grapher.RaiseEvent(new PointerPressedEventArgs(
            grapher,
            pointer,
            grapher,
            point,
            timestamp,
            new PointerPointProperties(
                RawInputModifiers.LeftMouseButton,
                PointerUpdateKind.LeftButtonPressed),
            KeyModifiers.None));

    private static void RaiseMoved(Grapher grapher, Pointer pointer, Point point, ulong timestamp) =>
        grapher.RaiseEvent(new PointerEventArgs(
            InputElement.PointerMovedEvent,
            grapher,
            pointer,
            grapher,
            point,
            timestamp,
            new PointerPointProperties(
                RawInputModifiers.LeftMouseButton,
                PointerUpdateKind.Other),
            KeyModifiers.None));

    private static void RaiseReleased(Grapher grapher, Pointer pointer, Point point, ulong timestamp) =>
        grapher.RaiseEvent(new PointerReleasedEventArgs(
            grapher,
            pointer,
            grapher,
            point,
            timestamp,
            new PointerPointProperties(
                RawInputModifiers.None,
                PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None,
            MouseButton.Left));
}
