using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace Calculator.BrowserHost;

internal sealed class BrotliStaticFileServer
{
    private const string BrotliSuffix = ".br";
    private const string DefaultFile = "index.html";
    private readonly FileExtensionContentTypeProvider _contentTypes = new();
    private readonly IFileProvider _files;

    public BrotliStaticFileServer(IFileProvider files)
    {
        _files = files;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!IsFileRequest(context.Request.Method))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var relativePath = GetRelativePath(context.Request.Path);
        if (relativePath.EndsWith(BrotliSuffix, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!await TryServeBrotliAsync(context, relativePath).ConfigureAwait(false))
        {
            await next(context).ConfigureAwait(false);
        }
    }

    public async Task ServeFallbackAsync(HttpContext context)
    {
        if (!IsFileRequest(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        if (await TryServeBrotliAsync(context, DefaultFile).ConfigureAwait(false))
        {
            return;
        }

        var file = _files.GetFileInfo(DefaultFile);
        var physicalPath = file.PhysicalPath;
        if (!file.Exists || string.IsNullOrEmpty(physicalPath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await ServeFileAsync(context, file, physicalPath, DefaultFile, null).ConfigureAwait(false);
    }

    private async Task<bool> TryServeBrotliAsync(HttpContext context, string relativePath)
    {
        var file = _files.GetFileInfo(relativePath + BrotliSuffix);
        var physicalPath = file.PhysicalPath;
        if (!file.Exists || string.IsNullOrEmpty(physicalPath))
        {
            return false;
        }

        if (!AcceptsBrotli(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status406NotAcceptable;
            return true;
        }

        await ServeFileAsync(context, file, physicalPath, relativePath, "br").ConfigureAwait(false);
        return true;
    }

    private async Task ServeFileAsync(
        HttpContext context,
        IFileInfo file,
        string physicalPath,
        string originalPath,
        string? contentEncoding)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentLength = file.Length;
        context.Response.ContentType = ContentType(originalPath);
        context.Response.GetTypedHeaders().LastModified = file.LastModified;
        if (contentEncoding is not null)
        {
            context.Response.Headers[HeaderNames.ContentEncoding] = contentEncoding;
            context.Response.Headers[HeaderNames.Vary] = HeaderNames.AcceptEncoding;
        }

        if (HttpMethods.IsGet(context.Request.Method))
        {
            await context.Response.SendFileAsync(physicalPath).ConfigureAwait(false);
        }
    }

    private string ContentType(string path)
    {
        return _contentTypes.TryGetContentType(path, out var contentType)
            ? contentType
            : "application/octet-stream";
    }

    private static bool AcceptsBrotli(HttpRequest request)
    {
        var encodings = request.GetTypedHeaders().AcceptEncoding;
        if (encodings is null)
        {
            return false;
        }

        foreach (var encoding in encodings)
        {
            var value = encoding.Value.Value;
            var accepted = string.Equals(value, "br", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "*", StringComparison.Ordinal);
            if (accepted && encoding.Quality.GetValueOrDefault(1) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFileRequest(string method)
    {
        return HttpMethods.IsGet(method) || HttpMethods.IsHead(method);
    }

    private static string GetRelativePath(PathString path)
    {
        var value = path.Value;
        return string.IsNullOrEmpty(value) || value == "/"
            ? DefaultFile
            : value.TrimStart('/');
    }
}
