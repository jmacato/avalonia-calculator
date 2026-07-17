using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
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
}
