using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
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
    private readonly record struct PointerButtonInfo(
        RawInputModifiers Modifier,
        PointerUpdateKind PressedKind,
        PointerUpdateKind ReleasedKind,
        MouseButton MouseButton);

    private readonly HttpListener _listener = new();
    private readonly MainWindow _window;
    private readonly int _port;
    private readonly string _token;
    private readonly CancellationTokenSource _stopping = new();
    private readonly IPointer _mousePointer = CreatePointer(4242, PointerType.Mouse, true);
    private IPointer _touchPointer = CreatePointer(4243, PointerType.Touch, true);
    private IPointer _penPointer = CreatePointer(4244, PointerType.Pen, true);
    private int _nextTransientPointerId = 4245;
    private Task? _listenTask;

    private AutomationServer(MainWindow window, int port, string token)
    {
        _window = window;
        _port = port;
        _token = token;
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
            throw new InvalidOperationException(
                "CALCULATOR_AUTOMATION_TOKEN must contain at least 16 characters when the automation server is enabled.");
        }

        var server = new AutomationServer(window, port, token);
        server._listener.Start();
        server._listenTask = server.ListenAsync();
        return server;
    }

    public async ValueTask DisposeAsync()
    {
        _stopping.Cancel();
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

            await HandleAsync(context).ConfigureAwait(false);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            if (!string.Equals(
                    request.Headers["X-Calculator-Automation-Token"],
                    _token,
                    StringComparison.Ordinal))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                await WriteJsonAsync(
                    context.Response,
                    new AutomationErrorResponse("A valid automation token is required."),
                    AutomationJsonContext.Default.AutomationErrorResponse).ConfigureAwait(false);
                return;
            }

            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;

            if (request.HttpMethod == "GET" && path == "/health")
            {
                await WriteJsonAsync(
                    context.Response,
                    new AutomationHealthResponse("ok", Environment.ProcessId, _port),
                    AutomationJsonContext.Default.AutomationHealthResponse).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/tree")
            {
                var response = await Dispatcher.UIThread.InvokeAsync(BuildTree);
                await WriteJsonAsync(
                    context.Response,
                    response,
                    AutomationJsonContext.Default.AutomationTreeResponse).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/render")
            {
                await Dispatcher.UIThread.InvokeAsync(PrimeWindowRender);
                await WaitForAnimationFrameAsync().ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(PrimeWindowRender);
                await WaitForAnimationFrameAsync().ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
                var png = await Dispatcher.UIThread.InvokeAsync(RenderWindow);
                context.Response.ContentType = "image/png";
                context.Response.ContentLength64 = png.Length;
                await context.Response.OutputStream.WriteAsync(png).ConfigureAwait(false);
                context.Response.Close();
                return;
            }

            if (request.HttpMethod == "GET" && path == "/render/frame")
            {
                var png = await Dispatcher.UIThread.InvokeAsync(RenderWindow);
                context.Response.ContentType = "image/png";
                context.Response.ContentLength64 = png.Length;
                await context.Response.OutputStream.WriteAsync(png).ConfigureAwait(false);
                context.Response.Close();
                return;
            }

            if (request.HttpMethod == "POST" && path == "/events/pointer")
            {
                var body = await JsonSerializer.DeserializeAsync(
                    request.InputStream,
                    AutomationJsonContext.Default.AutomationPointerRequest).ConfigureAwait(false)
                    ?? throw new InvalidDataException("A pointer request body is required.");
                await Dispatcher.UIThread.InvokeAsync(() => SendPointer(body));
                await WriteOkAsync(context.Response).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/events/click")
            {
                var body = await JsonSerializer.DeserializeAsync(
                    request.InputStream,
                    AutomationJsonContext.Default.AutomationTargetRequest).ConfigureAwait(false)
                    ?? throw new InvalidDataException("A click target is required.");
                await Dispatcher.UIThread.InvokeAsync(() => ClickTarget(body.Target));
                await WriteOkAsync(context.Response).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/events/key")
            {
                var body = await JsonSerializer.DeserializeAsync(
                    request.InputStream,
                    AutomationJsonContext.Default.AutomationKeyRequest).ConfigureAwait(false)
                    ?? throw new InvalidDataException("A key request body is required.");
                await Dispatcher.UIThread.InvokeAsync(() => SendKey(body));
                await WriteOkAsync(context.Response).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/events/text")
            {
                var body = await JsonSerializer.DeserializeAsync(
                    request.InputStream,
                    AutomationJsonContext.Default.AutomationTextRequest).ConfigureAwait(false)
                    ?? throw new InvalidDataException("A text request body is required.");
                await Dispatcher.UIThread.InvokeAsync(() => SendText(body.Text));
                await WriteOkAsync(context.Response).ConfigureAwait(false);
                return;
            }

            if (request.HttpMethod == "POST" && path == "/window/size")
            {
                var body = await JsonSerializer.DeserializeAsync(
                    request.InputStream,
                    AutomationJsonContext.Default.AutomationWindowSizeRequest).ConfigureAwait(false)
                    ?? throw new InvalidDataException("A window-size request body is required.");
                await Dispatcher.UIThread.InvokeAsync(() => ResizeWindow(body));
                await WaitForAnimationFrameAsync().ConfigureAwait(false);
                await WriteOkAsync(context.Response).ConfigureAwait(false);
                return;
            }

            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            await WriteJsonAsync(
                context.Response,
                new AutomationErrorResponse("Unknown automation endpoint."),
                AutomationJsonContext.Default.AutomationErrorResponse).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await WriteJsonAsync(
                context.Response,
                new AutomationErrorResponse(exception.Message),
                AutomationJsonContext.Default.AutomationErrorResponse).ConfigureAwait(false);
        }
    }

    private AutomationTreeResponse BuildTree()
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
        elements.Add(new AutomationElementInfo(
            control.GetType().Name,
            control.Name,
            AutomationProperties.GetAutomationId(control),
            AutomationProperties.GetName(control),
            AutomationProperties.GetAccessibilityView(control).ToString(),
            AutomationProperties.GetHeadingLevel(control),
            AutomationProperties.GetLandmarkType(control)?.ToString(),
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            control.DesiredSize.Width,
            control.DesiredSize.Height,
            IsEffectivelyVisible(control),
            control.IsEffectivelyEnabled,
            control.IsFocused,
            GetEffectiveOpacity(control),
            GetFontSize(control),
            control is FAProgressRing progressRing ? progressRing.IsActive : null,
            control is ComboBox comboBox ? comboBox.IsDropDownOpen : null,
            control is CalculatorApp.Controls.ConverterComboBox converterComboBox
                ? converterComboBox.IsPopupOpen
                : null,
            control is ComboBox indexedComboBox ? indexedComboBox.SelectedIndex : null,
            control is ScrollViewer scrollViewer ? scrollViewer.Offset.X : null,
            control is ScrollViewer offsetScrollViewer ? offsetScrollViewer.Offset.Y : null,
            control is ScrollViewer extentScrollViewer ? extentScrollViewer.Extent.Width : null,
            control is ScrollViewer heightExtentScrollViewer ? heightExtentScrollViewer.Extent.Height : null,
            control is ScrollViewer viewportScrollViewer ? viewportScrollViewer.Viewport.Width : null,
            control is ScrollViewer heightViewportScrollViewer ? heightViewportScrollViewer.Viewport.Height : null,
            control is Popup popup ? popup.HorizontalOffset : null,
            control is Popup offsetPopup ? offsetPopup.VerticalOffset : null,
            control is TextBlock textBlock ? textBlock.Text : null,
            control.Classes.Count > 0 ? string.Join(' ', control.Classes) : null));
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

    private static double? GetFontSize(Control control) => control switch
    {
        TextBlock textBlock => textBlock.FontSize,
        TemplatedControl templatedControl => templatedControl.FontSize,
        _ => null
    };

    private byte[] RenderWindow()
    {
        var size = GetRenderSize();
        using var windowBitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        windowBitmap.Render(_window);
        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        using (var drawingContext = bitmap.CreateDrawingContext())
        {
            drawingContext.DrawImage(windowBitmap, new Rect(windowBitmap.Size));
            foreach (var popupChild in EnumerateOpenPopupChildren())
            {
                Rect bounds = GetWindowBounds(popupChild);
                var popupSize = new PixelSize(
                    Math.Max(1, (int)Math.Ceiling(popupChild.Bounds.Width)),
                    Math.Max(1, (int)Math.Ceiling(popupChild.Bounds.Height)));
                using var popupBitmap = new RenderTargetBitmap(popupSize, new Vector(96, 96));
                popupBitmap.Render(popupChild);
                drawingContext.DrawImage(
                    popupBitmap,
                    new Rect(popupBitmap.Size),
                    new Rect(bounds.Position, popupBitmap.Size));
            }
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }

    private void PrimeWindowRender()
    {
        using var warmup = new RenderTargetBitmap(GetRenderSize(), new Vector(96, 96));
        warmup.Render(_window);
        foreach (var popupChild in EnumerateOpenPopupChildren())
        {
            var popupSize = new PixelSize(
                Math.Max(1, (int)Math.Ceiling(popupChild.Bounds.Width)),
                Math.Max(1, (int)Math.Ceiling(popupChild.Bounds.Height)));
            using var popupWarmup = new RenderTargetBitmap(popupSize, new Vector(96, 96));
            popupWarmup.Render(popupChild);
        }
    }

    private async Task WaitForAnimationFrameAsync()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Dispatcher.UIThread.InvokeAsync(() =>
            _window.RequestAnimationFrame(_ => completion.TrySetResult()));
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
    }

    private PixelSize GetRenderSize() => new(
        Math.Max(1, (int)Math.Ceiling(_window.Bounds.Width)),
        Math.Max(1, (int)Math.Ceiling(_window.Bounds.Height)));

    private void ClickTarget(string target)
    {
        var control = EnumerateControls().FirstOrDefault(candidate =>
            string.Equals(candidate.Name, target, StringComparison.Ordinal) ||
            string.Equals(AutomationProperties.GetAutomationId(candidate), target, StringComparison.Ordinal));
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
        var controls = new[] { _window }
            .Concat(_window.GetVisualDescendants().OfType<Control>())
            .ToList();
        var seen = new HashSet<Control>(ReferenceEqualityComparer.Instance);

        foreach (var control in controls)
        {
            if (seen.Add(control) && (includeHidden || IsEffectivelyVisible(control)))
            {
                yield return control;
            }
        }

        foreach (var popupChild in controls
                     .OfType<Popup>()
                     .Where(popup => popup.IsOpen)
                     .Select(popup => popup.Child)
                     .OfType<Control>())
        {
            foreach (var control in new[] { popupChild }
                         .Concat(popupChild.GetLogicalDescendants().OfType<Control>())
                         .Concat(popupChild.GetVisualDescendants().OfType<Control>()))
            {
                if (seen.Add(control) && (includeHidden || IsEffectivelyVisible(control)))
                {
                    yield return control;
                }
            }
        }

        var seenMenus = new HashSet<ContextMenu>(ReferenceEqualityComparer.Instance);
        foreach (var menu in controls
                     .Select(control => control.ContextMenu)
                     .OfType<ContextMenu>()
                     .Where(menu => menu.IsOpen && seenMenus.Add(menu)))
        {
            foreach (var control in new[] { menu }
                         .Concat(menu.GetLogicalDescendants().OfType<Control>())
                         .Concat(menu.GetVisualDescendants().OfType<Control>()))
            {
                if (seen.Add(control) && (includeHidden || IsEffectivelyVisible(control)))
                {
                    yield return control;
                }
            }
        }
    }

    private IEnumerable<Control> EnumerateOpenPopupChildren() =>
        new[] { _window }
            .Concat(_window.GetVisualDescendants().OfType<Control>())
            .OfType<Popup>()
            .Where(popup => popup.IsOpen)
            .Select(popup => popup.Child)
            .OfType<Control>();

    private Rect GetWindowBounds(Control control)
    {
        Point[] corners =
        [
            default,
            new Point(control.Bounds.Width, 0),
            new Point(0, control.Bounds.Height),
            new Point(control.Bounds.Width, control.Bounds.Height)
        ];
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

        if (TopLevel.GetTopLevel(control) is { } topLevel
            && control.TranslatePoint(point, topLevel) is { } topLevelPoint)
        {
            return _window.PointToClient(topLevel.PointToScreen(topLevelPoint));
        }

        return null;
    }

    private static bool IsEffectivelyVisible(Control control) =>
        control.IsVisible && control.GetVisualAncestors().OfType<Control>().All(ancestor => ancestor.IsVisible);

    private void SendPointer(AutomationPointerRequest request)
    {
        var point = new Point(request.X, request.Y);
        var modifiers = ParseModifiers(request.Modifiers);
        var keyModifiers = ToKeyModifiers(modifiers);
        IPointer pointer = GetPointer(request.PointerType);
        GestureRecognizer? capturedGesture = pointer is Pointer concretePointer
            ? GetCapturedGestureRecognizer(concretePointer)
            : null;
        Interactive? gestureTarget = capturedGesture is null
            ? null
            : GetGestureTarget(capturedGesture) as Interactive;
        // Real platform input is routed to the pointer capture target after a
        // drag begins. Preserve that behavior for multi-request gestures such
        // as the official SwipeControl instead of hit-testing every move as a
        // new, unrelated event.
        var target = gestureTarget
                     ?? pointer.Captured as Interactive
                     ?? HitTestWindowPoint(point)
                     ?? throw new InvalidOperationException($"No Avalonia input element exists at {point}.");
        var input = GetInputRootPoint(target, point);
        Point rootPoint = input.Point;
        switch (request.Kind.ToLowerInvariant())
        {
            case "move":
                var moved = new PointerEventArgs(
                    InputElement.PointerMovedEvent,
                    target,
                    pointer,
                    input.Root,
                    rootPoint,
                    Timestamp(),
                    new PointerPointProperties(modifiers, PointerUpdateKind.Other),
                    keyModifiers);
                if (capturedGesture is null)
                {
                    target.RaiseEvent(moved);
                }
                else
                {
                    RaiseGesturePointerMoved(capturedGesture, moved);
                }
                break;
            case "down":
                var pressedButton = ParsePointerButton(request.Button);
                target.RaiseEvent(new PointerPressedEventArgs(
                    target,
                    pointer,
                    input.Root,
                    rootPoint,
                    Timestamp(),
                    new PointerPointProperties(
                        modifiers | pressedButton.Modifier,
                        pressedButton.PressedKind),
                    keyModifiers,
                    1));
                break;
            case "up":
                RaisePointerRelease(
                    target,
                    point,
                    modifiers,
                    keyModifiers,
                    ParsePointerButton(request.Button),
                    pointer,
                    capturedGesture);
                CompleteTransientPointer(pointer);
                break;
            case "click":
                RaisePointerClick(
                    target,
                    point,
                    modifiers,
                    ParsePointerButton(request.Button),
                    pointer);
                CompleteTransientPointer(pointer);
                break;
            case "wheel":
                double deltaX = request.DeltaX ?? 0;
                double deltaY = request.DeltaY ?? 0;
                if (!double.IsFinite(deltaX)
                    || !double.IsFinite(deltaY)
                    || (deltaX == 0 && deltaY == 0))
                {
                    throw new InvalidDataException(
                        "A wheel request requires a finite, non-zero deltaX or deltaY.");
                }

                target.RaiseEvent(new PointerWheelEventArgs(
                    target,
                    pointer,
                    input.Root,
                    rootPoint,
                    Timestamp(),
                    new PointerPointProperties(modifiers, PointerUpdateKind.Other),
                    keyModifiers,
                    new Vector(deltaX, deltaY)));
                break;
            default:
                throw new InvalidDataException(
                    "Pointer kind must be move, down, up, click, or wheel.");
        }
    }

    private void RaisePointerRelease(
        Interactive target,
        Point point,
        RawInputModifiers modifiers,
        KeyModifiers keyModifiers,
        PointerButtonInfo button,
        IPointer pointer,
        GestureRecognizer? capturedGesture = null)
    {
        var input = GetInputRootPoint(target, point);
        var released = new PointerReleasedEventArgs(
            target,
            pointer,
            input.Root,
            input.Point,
            Timestamp(),
            new PointerPointProperties(modifiers, button.ReleasedKind),
            keyModifiers,
            button.MouseButton);
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
            Interactive contextTarget = new[] { target }
                .OfType<Control>()
                .Concat(target.GetVisualAncestors().OfType<Control>())
                .FirstOrDefault(control => control.ContextMenu is not null)
                ?? target;
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
        RaisePointerClick(
            target,
            point,
            modifiers,
            ParsePointerButton(null),
            _mousePointer);
    }

    private void RaisePointerClick(
        Interactive target,
        Point point,
        RawInputModifiers modifiers,
        PointerButtonInfo button,
        IPointer pointer)
    {
        var input = GetInputRootPoint(target, point);
        var keyModifiers = ToKeyModifiers(modifiers);
        target.RaiseEvent(new PointerEventArgs(
            InputElement.PointerMovedEvent,
            target,
            pointer,
            input.Root,
            input.Point,
            Timestamp(),
            new PointerPointProperties(modifiers, PointerUpdateKind.Other),
            keyModifiers));
        target.RaiseEvent(new PointerPressedEventArgs(
            target,
            pointer,
            input.Root,
            input.Point,
            Timestamp(),
            new PointerPointProperties(
                modifiers | button.Modifier,
                button.PressedKind),
            keyModifiers,
            1));
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
            if (visual is not Interactive target
                || visual is not IInputElement inputElement
                || !inputElement.IsHitTestVisible
                || !inputElement.IsEffectivelyEnabled
                || visual.GetTransformedBounds() is not { } bounds
                || !bounds.Clip.Contains(popupPoint)
                || !bounds.Contains(popupPoint))
            {
                continue;
            }

            bool blockedByAncestor = visual
                .GetSelfAndVisualAncestors()
                .TakeWhile(ancestor => !ReferenceEquals(ancestor, popupChild))
                .Any(ancestor => !ancestor.IsVisible
                    || ancestor is IInputElement
                    {
                        IsHitTestVisible: false
                    });
            if (!blockedByAncestor)
            {
                return target;
            }
        }

        return null;
    }

    private (TopLevel Root, Point Point) GetInputRootPoint(
        Interactive target,
        Point windowPoint)
    {
        TopLevel root = target is Visual visual
            ? TopLevel.GetTopLevel(visual) ?? _window
            : _window;
        Point rootPoint = ReferenceEquals(root, _window)
            ? windowPoint
            : root.PointToClient(_window.PointToScreen(windowPoint));
        return (root, rootPoint);
    }

    private static PointerButtonInfo ParsePointerButton(string? value) =>
        value?.ToLowerInvariant() switch
        {
            null or "" or "left" => new PointerButtonInfo(
                RawInputModifiers.LeftMouseButton,
                PointerUpdateKind.LeftButtonPressed,
                PointerUpdateKind.LeftButtonReleased,
                MouseButton.Left),
            "right" => new PointerButtonInfo(
                RawInputModifiers.RightMouseButton,
                PointerUpdateKind.RightButtonPressed,
                PointerUpdateKind.RightButtonReleased,
                MouseButton.Right),
            "middle" => new PointerButtonInfo(
                RawInputModifiers.MiddleMouseButton,
                PointerUpdateKind.MiddleButtonPressed,
                PointerUpdateKind.MiddleButtonReleased,
                MouseButton.Middle),
            _ => throw new InvalidDataException("Pointer button must be left, right, or middle.")
        };

    private IPointer GetPointer(string? value) =>
        value?.ToLowerInvariant() switch
        {
            null or "" or "mouse" => _mousePointer,
            "touch" => _touchPointer,
            "pen" => _penPointer,
            _ => throw new InvalidDataException(
                "Pointer type must be mouse, touch, or pen.")
        };

    private void CompleteTransientPointer(IPointer pointer)
    {
        if (ReferenceEquals(pointer, _touchPointer))
        {
            pointer.Capture(null);
            _touchPointer = CreatePointer(
                _nextTransientPointerId++,
                PointerType.Touch,
                true);
        }
        else if (ReferenceEquals(pointer, _penPointer))
        {
            pointer.Capture(null);
            _penPointer = CreatePointer(
                _nextTransientPointerId++,
                PointerType.Pen,
                true);
        }
    }

    private void SendKey(AutomationKeyRequest request)
    {
        if (!Enum.TryParse<Key>(request.Key, true, out var key))
        {
            throw new InvalidDataException($"Unknown Avalonia key '{request.Key}'.");
        }

        var modifiers = ParseModifiers(request.Modifiers);
        var target = _window.FocusManager.GetFocusedElement() as Interactive ?? _window;
        target.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = ToKeyModifiers(modifiers),
            PhysicalKey = PhysicalKey.None,
            KeySymbol = request.Text,
        });
        target.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyUpEvent,
            Key = key,
            KeyModifiers = ToKeyModifiers(modifiers),
            PhysicalKey = PhysicalKey.None,
            KeySymbol = request.Text,
        });
    }

    private void SendText(string text)
    {
        var target = _window.FocusManager.GetFocusedElement() as Interactive ?? _window;
        target.RaiseEvent(new TextInputEventArgs
        {
            RoutedEvent = InputElement.TextInputEvent,
            Text = text,
        });
    }

    private void ResizeWindow(AutomationWindowSizeRequest request)
    {
        if (!double.IsFinite(request.Width)
            || !double.IsFinite(request.Height)
            || request.Width < _window.MinWidth
            || request.Height < _window.MinHeight)
        {
            throw new InvalidDataException(
                $"Window size must be finite and at least {_window.MinWidth}x{_window.MinHeight}.");
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

    private static ulong Timestamp() => unchecked((ulong)Environment.TickCount64);

    private static Task WriteOkAsync(HttpListenerResponse response) =>
        WriteJsonAsync(
            response,
            new AutomationActionResponse("ok"),
            AutomationJsonContext.Default.AutomationActionResponse);

    private static async Task WriteJsonAsync<T>(
        HttpListenerResponse response,
        T value,
        JsonTypeInfo<T> typeInfo)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
        response.ContentType = "application/json; charset=utf-8";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern Pointer CreatePointer(int id, PointerType type, bool isPrimary);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_CapturedGestureRecognizer")]
    private static extern GestureRecognizer? GetCapturedGestureRecognizer(Pointer pointer);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "get_Target")]
    private static extern IInputElement? GetGestureTarget(GestureRecognizer gestureRecognizer);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "PointerMovedInternal")]
    private static extern void RaiseGesturePointerMoved(
        GestureRecognizer gestureRecognizer,
        PointerEventArgs eventArgs);

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "PointerReleasedInternal")]
    private static extern void RaiseGesturePointerReleased(
        GestureRecognizer gestureRecognizer,
        PointerReleasedEventArgs eventArgs);
}
