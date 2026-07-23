using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.FileProviders;

namespace Calculator.BrowserHost;

internal static class BrowserHostStaticAssets
{
    public static string? GetWebRootPath(BrowserHostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.UsesPublishEndpoints)
        {
            return null;
        }

        var publishDirectory = Path.GetDirectoryName(settings.StaticWebAssetsManifest)
            ?? throw new InvalidOperationException("The publish endpoints manifest must have a parent directory.");
        var publishedWebRoot = Path.Combine(publishDirectory, "wwwroot");
        return Directory.Exists(publishedWebRoot)
            ? publishedWebRoot
            : throw new DirectoryNotFoundException($"Published web root not found: {publishedWebRoot}");
    }

    public static void ConfigureBuilder(WebApplicationBuilder builder, BrowserHostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.UsesPublishEndpoints)
        {
            return;
        }

        builder.Configuration[WebHostDefaults.StaticWebAssetsKey] = settings.StaticWebAssetsManifest;
        StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
    }

    public static bool TryRedirectDocumentRequest(HttpContext context, BrowserHostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        if (!HttpMethods.IsGet(context.Request.Method)
            || (context.Request.Path != "/" && context.Request.Path != "/index.html"))
        {
            return false;
        }

        var publishedRoot = settings.UsesPublishEndpoints && context.Request.Path == "/";
        var enableTelemetry = !context.Request.Query.ContainsKey("telemetry");
        if (!publishedRoot && !enableTelemetry)
        {
            return false;
        }

        var path = publishedRoot ? "/index.html" : context.Request.Path.Value;
        var separator = context.Request.QueryString.HasValue ? "&" : "?";
        var telemetry = enableTelemetry ? $"{separator}telemetry=1" : string.Empty;
        context.Response.Redirect($"{context.Request.PathBase}{path}{context.Request.QueryString}{telemetry}");
        return true;
    }

    public static void Map(WebApplication app, IFileProvider files, BrowserHostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.UsesPublishEndpoints)
        {
            app.MapStaticAssets(settings.StaticWebAssetsManifest);
            return;
        }

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = files,
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream",
        });
    }
}
