using Calculator.BrowserHost;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

var settings = BrowserHostSettings.Parse(args);
var builder = WebApplication.CreateSlimBuilder();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel", LogLevel.Critical);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 256 * 1024;
    options.ListenAnyIP(settings.Port, listen => listen.UseHttps());
});
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = TelemetryJson.Options.PropertyNamingPolicy;
    options.SerializerOptions.DictionaryKeyPolicy = TelemetryJson.Options.DictionaryKeyPolicy;
    options.SerializerOptions.TypeInfoResolver = TelemetryJson.Options.TypeInfoResolver;
});
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(static _ => new TelemetryStore());
builder.Services.AddSingleton(static serviceProvider => new TelemetryLogWriter(
    serviceProvider.GetRequiredService<TelemetryStore>(),
    serviceProvider.GetRequiredService<BrowserHostSettings>(),
    serviceProvider.GetRequiredService<ILogger<TelemetryLogWriter>>()));
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<TelemetryLogWriter>());
builder.Services.AddHostedService(static serviceProvider => new TelemetryMonitor(
    serviceProvider.GetRequiredService<TelemetryStore>(),
    serviceProvider.GetRequiredService<ILogger<TelemetryMonitor>>()));
var app = builder.Build();
var files = new PhysicalFileProvider(settings.WebRoot);
var brotliFiles = new BrotliStaticFileServer(files);
app.Use(async (context, next) =>
{
    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
    context.Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
    var path = context.Request.Path;
    var isDocument = path == "/" || path == "/index.html" || !Path.HasExtension(path.Value);
    var isTelemetry = path.StartsWithSegments("/telemetry", StringComparison.Ordinal);
    var isProfileUpload = path == "/aot-profile";
    context.Response.Headers.CacheControl = isDocument || isTelemetry || isProfileUpload
        ? "no-store"
        : context.Request.Query.ContainsKey("v")
            ? "public,max-age=31536000,immutable"
            : "public,max-age=300";
    if (HttpMethods.IsGet(context.Request.Method)
        && (context.Request.Path == "/" || context.Request.Path == "/index.html")
        && !context.Request.Query.ContainsKey("telemetry"))
    {
        var separator = context.Request.QueryString.HasValue ? "&" : "?";
        context.Response.Redirect($"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}{separator}telemetry=1");
        return;
    }

    if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path == "/aot-profile")
    {
        var profilePath = Environment.GetEnvironmentVariable("CALCULATOR_AOT_PROFILE_PATH");
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var output = new FileStream(profilePath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
        await using (output.ConfigureAwait(false))
        {
            await context.Request.Body.CopyToAsync(output, context.RequestAborted).ConfigureAwait(false);
        }
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return;
    }

    await next().ConfigureAwait(false);
});
app.MapPost("/telemetry/v1/batch", async (HttpContext context, TelemetryStore store, IOptions<JsonOptions> jsonOptions) =>
{
    if (context.Request.ContentLength is > 256 * 1024)
    {
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    }

    TelemetryBatch? batch;
    try
    {
        batch = await context.Request.ReadFromJsonAsync<TelemetryBatch>(jsonOptions.Value.SerializerOptions, context.RequestAborted).ConfigureAwait(false);
    }
    catch (Exception exception) when (exception is BadHttpRequestException or System.Text.Json.JsonException)
    {
        return Results.BadRequest(new TelemetryErrorResponse("Malformed telemetry batch."));
    }

    if (batch is null)
    {
        return Results.BadRequest(new TelemetryErrorResponse("Telemetry batch is required."));
    }

    var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return store.TryIngest(batch, remoteAddress, out var error)
        ? Results.NoContent()
        : Results.BadRequest(new TelemetryErrorResponse(error ?? "Invalid telemetry batch."));
});
app.MapGet("/telemetry/v1/health", (TelemetryStore store, TelemetryLogWriter logWriter) => Results.Json(
    new TelemetryHealthResponse(
        "ok",
        store.GetSummaries(DateTimeOffset.UtcNow).Length,
        logWriter.LogPath,
        DateTimeOffset.UtcNow)));
app.MapGet("/telemetry/v1/sessions", (TelemetryStore store) => Results.Json(store.GetSummaries(DateTimeOffset.UtcNow)));
app.MapGet("/telemetry/v1/sessions/{sessionId}", (string sessionId, TelemetryStore store) => store.GetDetail(sessionId, DateTimeOffset.UtcNow) is { } detail ? Results.Json(detail) : Results.NotFound());
app.MapGet("/telemetry/v1/sessions/{sessionId}/inputs", (string sessionId, TelemetryStore store) => store.GetInputTrace(sessionId, DateTimeOffset.UtcNow) is { } trace ? Results.Json(trace) : Results.NotFound());
app.MapDelete("/telemetry/v1/sessions", (TelemetryStore store) => Results.Json(
    new TelemetryClearSessionsResponse(store.ClearSessions())));
app.MapGet("/telemetry", () => Results.Content(TelemetryDashboard.Html, "text/html; charset=utf-8"));
app.Use((HttpContext context, RequestDelegate next) => brotliFiles.InvokeAsync(context, next));
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
app.UseStaticFiles(new StaticFileOptions { FileProvider = files, ServeUnknownFileTypes = true, DefaultContentType = "application/octet-stream" });
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/telemetry", StringComparison.Ordinal))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await brotliFiles.ServeFallbackAsync(context).ConfigureAwait(false);
});
Console.WriteLine($"Calculator browser host: https://0.0.0.0:{settings.Port}");
Console.WriteLine($"Web root: {settings.WebRoot}");
Console.WriteLine($"Telemetry dashboard: https://127.0.0.1:{settings.Port}/telemetry");
Console.WriteLine($"Telemetry directory: {settings.TelemetryDirectory}");
await app.RunAsync().ConfigureAwait(false);
