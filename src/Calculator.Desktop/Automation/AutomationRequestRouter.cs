using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Avalonia.Threading;

namespace CalculatorApp.Automation;

internal sealed class AutomationRequestRouter(AutomationServer server, string token, int port)
{
    public async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            await RouteAsync(context).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            await WriteBadRequestAsync(context, exception).ConfigureAwait(false);
        }
        catch (InvalidDataException exception)
        {
            await WriteBadRequestAsync(context, exception).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            await WriteBadRequestAsync(context, exception).ConfigureAwait(false);
        }
        catch (InvalidOperationException exception)
        {
            await WriteBadRequestAsync(context, exception).ConfigureAwait(false);
        }
    }

    private async Task RouteAsync(HttpListenerContext context)
    {
        HttpListenerRequest request = context.Request;
        if (!string.Equals(request.Headers["X-Calculator-Automation-Token"], token, StringComparison.Ordinal))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await WriteJsonAsync(context.Response, new AutomationErrorResponse("A valid automation token is required."), AutomationJsonContext.Default.AutomationErrorResponse).ConfigureAwait(false);
            return;
        }

        string path = request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;
        bool handled = request.HttpMethod switch
        {
            "GET" => await RouteGetAsync(context, path).ConfigureAwait(false),
            "POST" => await RoutePostAsync(context, path).ConfigureAwait(false),
            _ => false
        };
        if (!handled)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            await WriteJsonAsync(context.Response, new AutomationErrorResponse("Unknown automation endpoint."), AutomationJsonContext.Default.AutomationErrorResponse).ConfigureAwait(false);
        }
    }

    private async Task<bool> RouteGetAsync(HttpListenerContext context, string path)
    {
        switch (path)
        {
            case "/health":
                await WriteJsonAsync(context.Response, new AutomationHealthResponse("ok", Environment.ProcessId, port), AutomationJsonContext.Default.AutomationHealthResponse).ConfigureAwait(false);
                return true;
            case "/tree":
                AutomationTreeResponse response = await Dispatcher.UIThread.InvokeAsync(server.BuildTree);
                await WriteJsonAsync(context.Response, response, AutomationJsonContext.Default.AutomationTreeResponse).ConfigureAwait(false);
                return true;
            case "/render":
                await RenderSettledFrameAsync(context.Response).ConfigureAwait(false);
                return true;
            case "/render/frame":
                await RenderCurrentFrameAsync(context.Response).ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private async Task<bool> RoutePostAsync(HttpListenerContext context, string path)
    {
        switch (path)
        {
            case "/events/pointer":
                AutomationPointerRequest pointer = await JsonSerializer.DeserializeAsync(context.Request.InputStream, AutomationJsonContext.Default.AutomationPointerRequest).ConfigureAwait(false) ?? throw new InvalidDataException("A pointer request body is required.");
                await Dispatcher.UIThread.InvokeAsync(() => server.SendPointer(pointer));
                break;
            case "/events/click":
                AutomationTargetRequest target = await JsonSerializer.DeserializeAsync(context.Request.InputStream, AutomationJsonContext.Default.AutomationTargetRequest).ConfigureAwait(false) ?? throw new InvalidDataException("A click target is required.");
                await Dispatcher.UIThread.InvokeAsync(() => server.ClickTarget(target.Target));
                break;
            case "/events/key":
                AutomationKeyRequest key = await JsonSerializer.DeserializeAsync(context.Request.InputStream, AutomationJsonContext.Default.AutomationKeyRequest).ConfigureAwait(false) ?? throw new InvalidDataException("A key request body is required.");
                await Dispatcher.UIThread.InvokeAsync(() => server.SendKey(key));
                break;
            case "/events/text":
                AutomationTextRequest text = await JsonSerializer.DeserializeAsync(context.Request.InputStream, AutomationJsonContext.Default.AutomationTextRequest).ConfigureAwait(false) ?? throw new InvalidDataException("A text request body is required.");
                await Dispatcher.UIThread.InvokeAsync(() => server.SendText(text.Text));
                break;
            case "/window/size":
                AutomationWindowSizeRequest size = await JsonSerializer.DeserializeAsync(context.Request.InputStream, AutomationJsonContext.Default.AutomationWindowSizeRequest).ConfigureAwait(false) ?? throw new InvalidDataException("A window-size request body is required.");
                await Dispatcher.UIThread.InvokeAsync(() => server.ResizeWindow(size));
                await server.WaitForAnimationFrameAsync().ConfigureAwait(false);
                break;
            default:
                return false;
        }

        await WriteOkAsync(context.Response).ConfigureAwait(false);
        return true;
    }

    private async Task RenderSettledFrameAsync(HttpListenerResponse response)
    {
        await server.WaitForAnimationFrameAsync().ConfigureAwait(false);
        await CaptureCurrentFrameAsync().ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
        await server.WaitForAnimationFrameAsync().ConfigureAwait(false);
        await CaptureCurrentFrameAsync().ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
        await RenderCurrentFrameAsync(response).ConfigureAwait(false);
    }

    private async Task RenderCurrentFrameAsync(HttpListenerResponse response)
    {
        byte[] png = await CaptureCurrentFrameAsync().ConfigureAwait(false);
        response.ContentType = "image/png";
        response.ContentLength64 = png.Length;
        await response.OutputStream.WriteAsync(png).ConfigureAwait(false);
        response.Close();
    }

    private async Task<byte[]> CaptureCurrentFrameAsync()
    {
        return await Dispatcher.UIThread.InvokeAsync(server.RenderWindowAsync).ConfigureAwait(false);
    }

    private static Task WriteOkAsync(HttpListenerResponse response)
    {
        return WriteJsonAsync(response, new AutomationActionResponse("ok"),
            AutomationJsonContext.Default.AutomationActionResponse);
    }

    private static async Task WriteBadRequestAsync(HttpListenerContext context, Exception exception)
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        await WriteJsonAsync(context.Response, new AutomationErrorResponse(exception.Message), AutomationJsonContext.Default.AutomationErrorResponse).ConfigureAwait(false);
    }

    private static async Task WriteJsonAsync<T>(HttpListenerResponse response, T value, JsonTypeInfo<T> typeInfo)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
        response.ContentType = "application/json; charset=utf-8";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }
}
