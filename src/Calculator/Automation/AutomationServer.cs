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
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

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
    private readonly int _port;
    private readonly string _token;
    private readonly CancellationTokenSource _stopping = new();
    private readonly IPointer _pointer = CreatePointer(4242, PointerType.Mouse, true);
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
        AddElement(_window, elements);
        foreach (var control in _window.GetVisualDescendants().OfType<Control>())
        {
            AddElement(control, elements);
        }

        return new AutomationTreeResponse(elements);
    }

    private void AddElement(Control control, List<AutomationElementInfo> elements)
    {
        var origin = control.TranslatePoint(default, _window) ?? default;
        elements.Add(new AutomationElementInfo(
            control.GetType().Name,
            control.Name,
            AutomationProperties.GetAutomationId(control),
            AutomationProperties.GetName(control),
            origin.X,
            origin.Y,
            control.Bounds.Width,
            control.Bounds.Height,
            IsEffectivelyVisible(control),
            control.IsEffectivelyEnabled));
    }

    private byte[] RenderWindow()
    {
        var size = GetRenderSize();
        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        bitmap.Render(_window);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }

    private void PrimeWindowRender()
    {
        using var warmup = new RenderTargetBitmap(GetRenderSize(), new Vector(96, 96));
        warmup.Render(_window);
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

        Interactive activationTarget = control.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault() ?? control;
        var activationControl = (Control)activationTarget;
        if (activationControl is ListBoxItem listBoxItem
            && listBoxItem.GetVisualAncestors().OfType<ListBox>().FirstOrDefault() is { } listBox)
        {
            listBox.SelectedItem = listBoxItem.DataContext;
            return;
        }

        var origin = activationControl.TranslatePoint(default, _window)
                     ?? throw new InvalidOperationException($"Control '{target}' is not attached to the window.");
        var point = new Point(
            origin.X + activationControl.Bounds.Width / 2,
            origin.Y + activationControl.Bounds.Height / 2);
        if (control is Button button)
        {
            if (button is ToggleButton toggleButton)
            {
                toggleButton.IsChecked = toggleButton.IsChecked != true;
            }

            if (button.Command?.CanExecute(button.CommandParameter) == true)
            {
                button.Command.Execute(button.CommandParameter);
            }

            button.Flyout?.ShowAt(button);

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            return;
        }

        RaisePointerClick(activationTarget, point, RawInputModifiers.None);
    }

    private IEnumerable<Control> EnumerateControls()
    {
        yield return _window;
        foreach (var control in _window.GetVisualDescendants().OfType<Control>())
        {
            if (IsEffectivelyVisible(control))
            {
                yield return control;
            }
        }
    }

    private static bool IsEffectivelyVisible(Control control) =>
        control.IsVisible && control.GetVisualAncestors().OfType<Control>().All(ancestor => ancestor.IsVisible);

    private void SendPointer(AutomationPointerRequest request)
    {
        var point = new Point(request.X, request.Y);
        var modifiers = ParseModifiers(request.Modifiers);
        var keyModifiers = ToKeyModifiers(modifiers);
        // Real platform input is routed to the pointer capture target after a
        // drag begins. Preserve that behavior for multi-request gestures such
        // as the official SwipeControl instead of hit-testing every move as a
        // new, unrelated event.
        var target = _pointer.Captured as Interactive
                     ?? _window.InputHitTest(point) as Interactive
                     ?? throw new InvalidOperationException($"No Avalonia input element exists at {point}.");
        switch (request.Kind.ToLowerInvariant())
        {
            case "move":
                target.RaiseEvent(new PointerEventArgs(
                    InputElement.PointerMovedEvent,
                    target,
                    _pointer,
                    _window,
                    point,
                    Timestamp(),
                    new PointerPointProperties(modifiers, PointerUpdateKind.Other),
                    keyModifiers));
                break;
            case "down":
                target.RaiseEvent(new PointerPressedEventArgs(
                    target,
                    _pointer,
                    _window,
                    point,
                    Timestamp(),
                    new PointerPointProperties(
                        modifiers | RawInputModifiers.LeftMouseButton,
                        PointerUpdateKind.LeftButtonPressed),
                    keyModifiers,
                    1));
                break;
            case "up":
                target.RaiseEvent(new PointerReleasedEventArgs(
                    target,
                    _pointer,
                    _window,
                    point,
                    Timestamp(),
                    new PointerPointProperties(modifiers, PointerUpdateKind.LeftButtonReleased),
                    keyModifiers,
                    MouseButton.Left));
                break;
            case "click":
                RaisePointerClick(target, point, modifiers);
                break;
            default:
                throw new InvalidDataException("Pointer kind must be move, down, up, or click.");
        }
    }

    private void RaisePointerClick(Interactive target, Point point, RawInputModifiers modifiers)
    {
        var keyModifiers = ToKeyModifiers(modifiers);
        target.RaiseEvent(new PointerEventArgs(
            InputElement.PointerMovedEvent,
            target,
            _pointer,
            _window,
            point,
            Timestamp(),
            new PointerPointProperties(modifiers, PointerUpdateKind.Other),
            keyModifiers));
        target.RaiseEvent(new PointerPressedEventArgs(
            target,
            _pointer,
            _window,
            point,
            Timestamp(),
            new PointerPointProperties(
                modifiers | RawInputModifiers.LeftMouseButton,
                PointerUpdateKind.LeftButtonPressed),
            keyModifiers,
            1));
        target.RaiseEvent(new PointerReleasedEventArgs(
            target,
            _pointer,
            _window,
            point,
            Timestamp(),
            new PointerPointProperties(modifiers, PointerUpdateKind.LeftButtonReleased),
            keyModifiers,
            MouseButton.Left));
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
}
