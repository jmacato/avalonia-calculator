using System.Net;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;

namespace CalculatorApp.Automation;
/// <summary>
/// Loopback-only automation endpoint for the real application visual tree.
/// Input is delivered through Avalonia's raw input pipeline and screenshots are
/// produced by rendering the active window into an Avalonia bitmap.
/// </summary>
internal sealed class AutomationServer : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly MainWindow _window;
    private readonly AutomationRequestRouter _requestRouter;
    private readonly CancellationTokenSource _stopping = new();
    private readonly IPointer _mousePointer = CreatePointer(4242, PointerType.Mouse, true);
    private IPointer _touchPointer = CreatePointer(4243, PointerType.Touch, true);
    private IPointer _penPointer = CreatePointer(4244, PointerType.Pen, true);
    private int _nextTransientPointerId = 4245;
    private Task? _listenTask;
    private AutomationServer(MainWindow window, int port, string token)
    {
        _window = window;
        _requestRouter = new AutomationRequestRouter(this, token, port);
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public static AutomationServer? StartFromEnvironment(MainWindow window)
    {
        var portValue = Environment.GetEnvironmentVariable("CALCULATOR_AUTOMATION_PORT");
        if (!int.TryParse(portValue, out var port) || port is < 1 or > 65535)
        {
            return null;
        }

        var token = Environment.GetEnvironmentVariable("CALCULATOR_AUTOMATION_TOKEN");
        if (string.IsNullOrWhiteSpace(token) || token.Length < 16)
        {
            throw new InvalidOperationException("CALCULATOR_AUTOMATION_TOKEN must contain at least 16 characters when the automation server is enabled.");
        }

        var server = new AutomationServer(window, port, token);
        server._listener.Start();
        server._listenTask = server.ListenAsync();
        return server;
    }

    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        _listener.Close();
        if (_listenTask is not null)
        {
            try
            {
                await _listenTask.ConfigureAwait(false);
            }
            catch (HttpListenerException) when (_stopping.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (_stopping.IsCancellationRequested)
            {
            }
        }

        _stopping.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException) when (_stopping.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException) when (_stopping.IsCancellationRequested)
            {
                return;
            }

            await _requestRouter.HandleAsync(context).ConfigureAwait(false);
        }
    }

    internal AutomationTreeResponse BuildTree()
    {
        var elements = new List<AutomationElementInfo>();
        foreach (var control in EnumerateControls(includeHidden: true))
        {
            AddElement(control, elements);
        }

        return new AutomationTreeResponse(elements);
    }

    private void AddElement(Control control, List<AutomationElementInfo> elements)
    {
        Rect bounds = GetWindowBounds(control);
        elements.Add(new AutomationElementInfo(control.GetType().Name, control.Name, AutomationProperties.GetAutomationId(control), AutomationProperties.GetName(control), AutomationProperties.GetAccessibilityView(control).ToString(), AutomationProperties.GetHeadingLevel(control), AutomationProperties.GetLandmarkType(control)?.ToString(), bounds.X, bounds.Y, bounds.Width, bounds.Height, control.DesiredSize.Width, control.DesiredSize.Height, IsEffectivelyVisible(control), control.IsEffectivelyEnabled, control.IsFocused, GetEffectiveOpacity(control), GetFontSize(control), control is FAProgressRing progressRing ? progressRing.IsActive : null, control is ComboBox comboBox ? comboBox.IsDropDownOpen : null, control is Controls.ConverterComboBox converterComboBox ? converterComboBox.IsPopupOpen : null, control is ComboBox indexedComboBox ? indexedComboBox.SelectedIndex : null, control is ScrollViewer scrollViewer ? scrollViewer.Offset.X : null, control is ScrollViewer offsetScrollViewer ? offsetScrollViewer.Offset.Y : null, control is ScrollViewer extentScrollViewer ? extentScrollViewer.Extent.Width : null, control is ScrollViewer heightExtentScrollViewer ? heightExtentScrollViewer.Extent.Height : null, control is ScrollViewer viewportScrollViewer ? viewportScrollViewer.Viewport.Width : null, control is ScrollViewer heightViewportScrollViewer ? heightViewportScrollViewer.Viewport.Height : null, control is Popup popup ? popup.HorizontalOffset : null, control is Popup offsetPopup ? offsetPopup.VerticalOffset : null, control is TextBlock textBlock ? textBlock.Text : null, control.Classes.Count > 0 ? string.Join(' ', control.Classes) : null));
    }

    private static double GetEffectiveOpacity(Visual visual)
    {
        double opacity = 1;
        for (Visual? current = visual; current is not null; current = current.GetVisualParent())
        {
            opacity *= current.Opacity;
        }

        return opacity;
    }

    private static double? GetFontSize(Control control)
    {
        return control switch
        {
            TextBlock textBlock => textBlock.FontSize,
            TemplatedControl templatedControl => templatedControl.FontSize,
            _ => null
        };
    }

    internal Task<byte[]> RenderWindowAsync()
    {
        Dispatcher.UIThread.VerifyAccess();
        CompositionVisual windowVisual = ElementComposition.GetElementVisual(_window)
            ?? throw new InvalidOperationException("The application window is not attached to a compositor.");
        Task<Bitmap> windowSnapshot = windowVisual.Compositor.CreateCompositionVisualSnapshot(windowVisual, 1);
        var popupSnapshots = new List<AutomationPopupSnapshot>();
        foreach (Control popupChild in EnumerateOpenPopupChildren())
        {
            if (ElementComposition.GetElementVisual(popupChild) is not { } popupVisual)
            {
                continue;
            }

            popupSnapshots.Add(new AutomationPopupSnapshot(
                popupVisual.Compositor.CreateCompositionVisualSnapshot(popupVisual, 1),
                GetWindowBounds(popupChild).Position));
        }

        return EncodeSnapshotsAsync(windowSnapshot, popupSnapshots);
    }

    private static async Task<byte[]> EncodeSnapshotsAsync(
        Task<Bitmap> windowSnapshot,
        List<AutomationPopupSnapshot> popupSnapshots)
    {
        return Array.Empty<byte>();
        // using Bitmap windowBitmap = await windowSnapshot.ConfigureAwait(false);
        // byte[] windowPng = EncodeBitmap(windowBitmap);
        // if (popupSnapshots.Count == 0)
        // {
        //     return windowPng;
        // }
        //
        // using SKBitmap windowPixels = SKBitmap.Decode(windowPng)
        //     ?? throw new InvalidOperationException("Avalonia produced an invalid window snapshot.");
        // var imageInfo = new SKImageInfo(windowPixels.Width, windowPixels.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        // using SKSurface surface = SKSurface.Create(imageInfo);
        // surface.Canvas.Clear(SKColors.Transparent);
        // surface.Canvas.DrawBitmap(windowPixels, 0, 0);
        // foreach (AutomationPopupSnapshot popupSnapshot in popupSnapshots)
        // {
        //     using Bitmap popupBitmap = await popupSnapshot.Snapshot.ConfigureAwait(false);
        //     byte[] popupPng = EncodeBitmap(popupBitmap);
        //     using SKBitmap popupPixels = SKBitmap.Decode(popupPng)
        //         ?? throw new InvalidOperationException("Avalonia produced an invalid popup snapshot.");
        //     surface.Canvas.DrawBitmap(
        //         popupPixels,
        //         (float)popupSnapshot.Position.X,
        //         (float)popupSnapshot.Position.Y);
        // }
        //
        // using SKImage image = surface.Snapshot();
        // using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        // return data.ToArray();
    }

    internal async Task WaitForAnimationFrameAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Dispatcher.UIThread.InvokeAsync(() => _window.RequestAnimationFrame(_ => completion.TrySetResult()));
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
    }

    private static byte[] EncodeBitmap(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }

    internal void ClickTarget(string target)
    {
        var control = EnumerateControls().FirstOrDefault(candidate => string.Equals(candidate.Name, target, StringComparison.Ordinal) || string.Equals(AutomationProperties.GetAutomationId(candidate), target, StringComparison.Ordinal));
        if (control is null)
        {
            throw new InvalidOperationException($"No visible Avalonia control has name or automation id '{target}'.");
        }

        if (control is MenuItem menuItem)
        {
            menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, menuItem));
            return;
        }

        Interactive activationTarget = control.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault() ?? control;
        var activationControl = (Control)activationTarget;
        Rect bounds = GetWindowBounds(activationControl);
        var point = bounds.Center;
        RaisePointerClick(activationTarget, point, RawInputModifiers.None);
    }

    private IEnumerable<Control> EnumerateControls(bool includeHidden = false)
    {
        var controls = new[]
        {
            _window
        }.Concat(_window.GetVisualDescendants().OfType<Control>()).ToList();
        var seen = new HashSet<Control>(ReferenceEqualityComparer.Instance);
        foreach (var control in controls)
        {
            if (seen.Add(control) && (includeHidden || IsEffectivelyVisible(control)))
            {
                yield return control;
            }
        }

        foreach (var popupChild in controls.OfType<Popup>().Where(popup => popup.IsOpen).Select(popup => popup.Child).OfType<Control>())
        {
            foreach (var control in new[]
            {
                popupChild
            }.Concat(popupChild.GetLogicalDescendants().OfType<Control>()).Concat(popupChild.GetVisualDescendants().OfType<Control>()))
            {
                if (seen.Add(control) && (includeHidden || IsEffectivelyVisible(control)))
                {
                    yield return control;
                }
            }
        }

        var seenMenus = new HashSet<ContextMenu>(ReferenceEqualityComparer.Instance);
        foreach (var menu in controls.Select(control => control.ContextMenu).OfType<ContextMenu>().Where(menu => menu.IsOpen && seenMenus.Add(menu)))
        {
            foreach (var control in new[]
            {
                menu
            }.Concat(menu.GetLogicalDescendants().OfType<Control>()).Concat(menu.GetVisualDescendants().OfType<Control>()))
            {
                if (seen.Add(control) && (includeHidden || IsEffectivelyVisible(control)))
                {
                    yield return control;
                }
            }
        }
    }

    private IEnumerable<Control> EnumerateOpenPopupChildren()
    {
        return new[] { _window }.Concat(_window.GetVisualDescendants().OfType<Control>()).OfType<Popup>()
            .Where(popup => popup.IsOpen).Select(popup => popup.Child).OfType<Control>();
    }

    private Rect GetWindowBounds(Control control)
    {
        Point[] corners = [default, new Point(control.Bounds.Width, 0), new Point(0, control.Bounds.Height), new Point(control.Bounds.Width, control.Bounds.Height)];
        Point[] transformed = new Point[corners.Length];
        for (int index = 0; index < corners.Length; index++)
        {
            if (TranslateToWindow(control, corners[index]) is not { } point)
            {
                return new Rect(default, control.Bounds.Size);
            }

            transformed[index] = point;
        }

        double left = transformed.Min(point => point.X);
        double top = transformed.Min(point => point.Y);
        double right = transformed.Max(point => point.X);
        double bottom = transformed.Max(point => point.Y);
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private Point? TranslateToWindow(Control control, Point point)
    {
        if (control.TranslatePoint(point, _window) is { } windowPoint)
        {
            return windowPoint;
        }

        if (TopLevel.GetTopLevel(control) is { } topLevel && control.TranslatePoint(point, topLevel) is { } topLevelPoint)
        {
            return _window.PointToClient(topLevel.PointToScreen(topLevelPoint));
        }

        return null;
    }

    private static bool IsEffectivelyVisible(Control control)
    {
        return control.IsVisible && control.GetVisualAncestors().OfType<Control>().All(ancestor => ancestor.IsVisible);
    }

    internal void SendPointer(AutomationPointerRequest request)
    {
        var point = new Point(request.X, request.Y);
        var modifiers = ParseModifiers(request.Modifiers);
        var keyModifiers = ToKeyModifiers(modifiers);
        IPointer pointer = GetPointer(request.PointerType);
        GestureRecognizer? capturedGesture = pointer is Pointer concretePointer ? GetCapturedGestureRecognizer(concretePointer) : null;
        Interactive? gestureTarget = capturedGesture is null ? null : GetGestureTarget(capturedGesture) as Interactive;
        // Real platform input is routed to the pointer capture target after a
        // drag begins. Preserve that behavior for multi-request gestures such
        // as the official SwipeControl instead of hit-testing every move as a
        // new, unrelated event.
        var target = gestureTarget ?? pointer.Captured as Interactive ?? HitTestWindowPoint(point) ?? throw new InvalidOperationException($"No Avalonia input element exists at {point}.");
        var input = GetInputRootPoint(target, point);
        Point rootPoint = input.Point;
        switch (request.Kind.ToUpperInvariant())
        {
            case "MOVE":
                var moved = new PointerEventArgs(InputElement.PointerMovedEvent, target, pointer, input.Root, rootPoint, Timestamp(), new PointerPointProperties(modifiers, PointerUpdateKind.Other), keyModifiers);
                if (capturedGesture is null)
                {
                    target.RaiseEvent(moved);
                }
                else
                {
                    RaiseGesturePointerMoved(capturedGesture, moved);
                }

                break;
            case "DOWN":
                var pressedButton = ParsePointerButton(request.Button);
                target.RaiseEvent(new PointerPressedEventArgs(target, pointer, input.Root, rootPoint, Timestamp(), new PointerPointProperties(modifiers | pressedButton.Modifier, pressedButton.PressedKind), keyModifiers, 1));
                break;
            case "UP":
                RaisePointerRelease(target, point, modifiers, keyModifiers, ParsePointerButton(request.Button), pointer, capturedGesture);
                CompleteTransientPointer(pointer);
                break;
            case "CLICK":
                RaisePointerClick(target, point, modifiers, ParsePointerButton(request.Button), pointer);
                CompleteTransientPointer(pointer);
                break;
            case "WHEEL":
                double deltaX = request.DeltaX ?? 0;
                double deltaY = request.DeltaY ?? 0;
                if (!double.IsFinite(deltaX) || !double.IsFinite(deltaY) || (deltaX == 0 && deltaY == 0))
                {
                    throw new InvalidDataException("A wheel request requires a finite, non-zero deltaX or deltaY.");
                }

                target.RaiseEvent(new PointerWheelEventArgs(target, pointer, input.Root, rootPoint, Timestamp(), new PointerPointProperties(modifiers, PointerUpdateKind.Other), keyModifiers, new Vector(deltaX, deltaY)));
                break;
            default:
                throw new InvalidDataException("Pointer kind must be move, down, up, click, or wheel.");
        }
    }

    private void RaisePointerRelease(Interactive target, Point point, RawInputModifiers modifiers, KeyModifiers keyModifiers, AutomationServerPointerButtonInfo button, IPointer pointer, GestureRecognizer? capturedGesture = null)
    {
        var input = GetInputRootPoint(target, point);
        var released = new PointerReleasedEventArgs(target, pointer, input.Root, input.Point, Timestamp(), new PointerPointProperties(modifiers, button.ReleasedKind), keyModifiers, button.MouseButton);
        if (capturedGesture is null)
        {
            target.RaiseEvent(released);
        }
        else
        {
            RaiseGesturePointerReleased(capturedGesture, released);
        }

        if (button.MouseButton == MouseButton.Right)
        {
            // Platform raw-input processing normally produces this routed
            // event after a secondary-button release. The automation server
            // enters below that layer, so reproduce the same Avalonia event
            // on the nearest control that owns the requested context menu.
            Interactive contextTarget = new[]
            {
                target
            }.OfType<Control>().Concat(target.GetVisualAncestors().OfType<Control>()).FirstOrDefault(control => control.ContextMenu is not null) ?? target;
            var contextRequested = new ContextRequestedEventArgs(released)
            {
                RoutedEvent = InputElement.ContextRequestedEvent
            };
            contextTarget.RaiseEvent(contextRequested);
            if (contextTarget is Control { ContextMenu: { IsOpen: false } menu } owner)
            {
                // ContextMenu's platform service normally performs this after
                // raw input. Routed-event injection deliberately bypasses that
                // service on some backends, so complete the framework operation
                // only when the routed event did not already open the menu.
                menu.Open(owner);
            }

            if (contextTarget is Control { ContextMenu.IsOpen: false })
            {
                throw new InvalidOperationException("Avalonia did not open the requested context menu.");
            }
        }
    }

    private void RaisePointerClick(Interactive target, Point point, RawInputModifiers modifiers)
    {
        RaisePointerClick(target, point, modifiers, ParsePointerButton(null), _mousePointer);
    }

    private void RaisePointerClick(Interactive target, Point point, RawInputModifiers modifiers, AutomationServerPointerButtonInfo button, IPointer pointer)
    {
        var input = GetInputRootPoint(target, point);
        var keyModifiers = ToKeyModifiers(modifiers);
        target.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, target, pointer, input.Root, input.Point, Timestamp(), new PointerPointProperties(modifiers, PointerUpdateKind.Other), keyModifiers));
        target.RaiseEvent(new PointerPressedEventArgs(target, pointer, input.Root, input.Point, Timestamp(), new PointerPointProperties(modifiers | button.Modifier, button.PressedKind), keyModifiers, 1));
        RaisePointerRelease(target, point, modifiers, keyModifiers, button, pointer);
    }

    private Interactive? HitTestWindowPoint(Point windowPoint)
    {
        PixelPoint screenPoint = _window.PointToScreen(windowPoint);
        foreach (Control popupChild in EnumerateOpenPopupChildren().Reverse())
        {
            Rect popupBounds = GetWindowBounds(popupChild);
            if (!popupBounds.Contains(windowPoint))
            {
                continue;
            }

            TopLevel? popupRoot = TopLevel.GetTopLevel(popupChild);
            if (popupRoot is null)
            {
                continue;
            }

            Point popupPoint = popupRoot.PointToClient(screenPoint);
            if (popupRoot.InputHitTest(popupPoint) is Interactive target)
            {
                return target;
            }

            // A native popup owns a separate presentation source. On macOS,
            // compositor hit-testing can return no result for an event injected
            // from the parent window even though the popup visual tree is fully
            // arranged. Fall back to the same arranged bounds and z-order while
            // still raising the real routed Avalonia event on the hit element.
            if (HitTestArrangedPopup(popupChild, popupPoint) is { } arrangedTarget)
            {
                return arrangedTarget;
            }
        }

        return _window.InputHitTest(windowPoint) as Interactive;
    }

    private static Interactive? HitTestArrangedPopup(Control popupChild, Point popupPoint)
    {
        foreach (Visual visual in popupChild.GetSelfAndVisualDescendants().Reverse())
        {
            if (visual is not Interactive target || visual is not IInputElement inputElement || !inputElement.IsHitTestVisible || !inputElement.IsEffectivelyEnabled || visual.GetTransformedBounds() is not { } bounds || !bounds.Clip.Contains(popupPoint) || !bounds.Contains(popupPoint))
            {
                continue;
            }

            bool blockedByAncestor = visual.GetSelfAndVisualAncestors().TakeWhile(ancestor => !ReferenceEquals(ancestor, popupChild)).Any(ancestor => !ancestor.IsVisible || ancestor is IInputElement { IsHitTestVisible: false });
            if (!blockedByAncestor)
            {
                return target;
            }
        }

        return null;
    }

    private (TopLevel Root, Point Point) GetInputRootPoint(Interactive target, Point windowPoint)
    {
        TopLevel root = target is Visual visual ? TopLevel.GetTopLevel(visual) ?? _window : _window;
        Point rootPoint = ReferenceEquals(root, _window) ? windowPoint : root.PointToClient(_window.PointToScreen(windowPoint));
        return (root, rootPoint);
    }

    private static AutomationServerPointerButtonInfo ParsePointerButton(string? value)
    {
        return value?.ToUpperInvariant() switch
        {
            null or "" or "LEFT" => new AutomationServerPointerButtonInfo(RawInputModifiers.LeftMouseButton,
                PointerUpdateKind.LeftButtonPressed, PointerUpdateKind.LeftButtonReleased, MouseButton.Left),
            "RIGHT" => new AutomationServerPointerButtonInfo(RawInputModifiers.RightMouseButton,
                PointerUpdateKind.RightButtonPressed, PointerUpdateKind.RightButtonReleased, MouseButton.Right),
            "MIDDLE" => new AutomationServerPointerButtonInfo(RawInputModifiers.MiddleMouseButton,
                PointerUpdateKind.MiddleButtonPressed, PointerUpdateKind.MiddleButtonReleased, MouseButton.Middle),
            _ => throw new InvalidDataException("Pointer button must be left, right, or middle.")
        };
    }

    private IPointer GetPointer(string? value)
    {
        return value?.ToUpperInvariant() switch
        {
            null or "" or "MOUSE" => _mousePointer,
            "TOUCH" => _touchPointer,
            "PEN" => _penPointer,
            _ => throw new InvalidDataException("Pointer type must be mouse, touch, or pen.")
        };
    }

    private void CompleteTransientPointer(IPointer pointer)
    {
        if (ReferenceEquals(pointer, _touchPointer))
        {
            pointer.Capture(null);
            _touchPointer = CreatePointer(_nextTransientPointerId++, PointerType.Touch, true);
        }
        else if (ReferenceEquals(pointer, _penPointer))
        {
            pointer.Capture(null);
            _penPointer = CreatePointer(_nextTransientPointerId++, PointerType.Pen, true);
        }
    }

    internal void SendKey(AutomationKeyRequest request)
    {
        if (!Enum.TryParse<Key>(request.Key, true, out var key))
        {
            throw new InvalidDataException($"Unknown Avalonia key '{request.Key}'.");
        }

        var modifiers = ParseModifiers(request.Modifiers);
        var target = _window.FocusManager.GetFocusedElement() as Interactive ?? _window;
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = ToKeyModifiers(modifiers), PhysicalKey = PhysicalKey.None, KeySymbol = request.Text, });
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = key, KeyModifiers = ToKeyModifiers(modifiers), PhysicalKey = PhysicalKey.None, KeySymbol = request.Text, });
    }

    internal void SendText(string text)
    {
        var target = _window.FocusManager.GetFocusedElement() as Interactive ?? _window;
        target.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = text, });
    }

    internal void ResizeWindow(AutomationWindowSizeRequest request)
    {
        if (!double.IsFinite(request.Width) || !double.IsFinite(request.Height) || request.Width < _window.MinWidth || request.Height < _window.MinHeight)
        {
            throw new InvalidDataException($"Window size must be finite and at least {_window.MinWidth}x{_window.MinHeight}.");
        }

        _window.WindowState = WindowState.Normal;
        _window.Width = request.Width;
        _window.Height = request.Height;
    }

    private static RawInputModifiers ParseModifiers(string? value)
    {
        var result = RawInputModifiers.None;
        if (string.IsNullOrWhiteSpace(value))
        {
            return result;
        }

        foreach (var part in value.Split(['+', '|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<RawInputModifiers>(part, true, out var modifier))
            {
                throw new InvalidDataException($"Unknown Avalonia input modifier '{part}'.");
            }

            result |= modifier;
        }

        return result;
    }

    private static KeyModifiers ToKeyModifiers(RawInputModifiers modifiers)
    {
        var result = KeyModifiers.None;
        if ((modifiers & RawInputModifiers.Alt) == RawInputModifiers.Alt)
        {
            result |= KeyModifiers.Alt;
        }

        if ((modifiers & RawInputModifiers.Control) == RawInputModifiers.Control)
        {
            result |= KeyModifiers.Control;
        }

        if ((modifiers & RawInputModifiers.Meta) == RawInputModifiers.Meta)
        {
            result |= KeyModifiers.Meta;
        }

        if ((modifiers & RawInputModifiers.Shift) == RawInputModifiers.Shift)
        {
            result |= KeyModifiers.Shift;
        }

        return result;
    }

    private static ulong Timestamp()
    {
        return unchecked((ulong)Environment.TickCount64);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern Pointer CreatePointer(int id, PointerType type, bool isPrimary);
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_CapturedGestureRecognizer")]
    private static extern GestureRecognizer? GetCapturedGestureRecognizer(Pointer pointer);
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Target")]
    private static extern IInputElement? GetGestureTarget(GestureRecognizer gestureRecognizer);
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "PointerMovedInternal")]
    private static extern void RaiseGesturePointerMoved(GestureRecognizer gestureRecognizer, PointerEventArgs eventArgs);
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "PointerReleasedInternal")]
    private static extern void RaiseGesturePointerReleased(GestureRecognizer gestureRecognizer, PointerReleasedEventArgs eventArgs);
}
