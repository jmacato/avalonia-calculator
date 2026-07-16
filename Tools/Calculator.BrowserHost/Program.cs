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
    options.Limits.MaxRequestBodySize = 64 * 1024;
    options.ListenAnyIP(settings.Port, listen => listen.UseHttps());
});
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = TelemetryJson.Options.PropertyNamingPolicy;
    options.SerializerOptions.DictionaryKeyPolicy = TelemetryJson.Options.DictionaryKeyPolicy;
});
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<TelemetryStore>();
builder.Services.AddSingleton<TelemetryLogWriter>();
builder.Services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<TelemetryLogWriter>());
builder.Services.AddHostedService<TelemetryMonitor>();

var app = builder.Build();
var files = new PhysicalFileProvider(settings.WebRoot);

app.Use(async (context, next) =>
{
    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
    context.Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
    context.Response.Headers.CacheControl = "no-store";

    if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path == "/aot-profile")
    {
        var profilePath = Environment.GetEnvironmentVariable("CALCULATOR_AOT_PROFILE_PATH");
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await using var output = new FileStream(
            profilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            useAsync: true);
        await context.Request.Body.CopyToAsync(output, context.RequestAborted);
        context.Response.StatusCode = StatusCodes.Status204NoContent;
        return;
    }

    await next();
});

app.MapPost("/telemetry/v1/batch", async (
    HttpContext context,
    TelemetryStore store,
    IOptions<JsonOptions> jsonOptions) =>
{
    if (context.Request.ContentLength is > 64 * 1024)
    {
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    }

    TelemetryBatch? batch;
    try
    {
        batch = await context.Request.ReadFromJsonAsync<TelemetryBatch>(
            jsonOptions.Value.SerializerOptions,
            context.RequestAborted);
    }
    catch (Exception exception) when (exception is BadHttpRequestException or System.Text.Json.JsonException)
    {
        return Results.BadRequest(new { error = "Malformed telemetry batch." });
    }

    if (batch is null)
    {
        return Results.BadRequest(new { error = "Telemetry batch is required." });
    }

    var remoteAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    return store.TryIngest(batch, remoteAddress, out var error)
        ? Results.NoContent()
        : Results.BadRequest(new { error });
});

app.MapGet("/telemetry/v1/health", (TelemetryStore store, TelemetryLogWriter logWriter) =>
    Results.Json(new
    {
        status = "ok",
        sessions = store.GetSummaries(DateTimeOffset.UtcNow).Length,
        logPath = logWriter.LogPath,
        serverTime = DateTimeOffset.UtcNow
    }));

app.MapGet("/telemetry/v1/sessions", (TelemetryStore store) =>
    Results.Json(store.GetSummaries(DateTimeOffset.UtcNow)));

app.MapGet("/telemetry/v1/sessions/{sessionId}", (string sessionId, TelemetryStore store) =>
    store.GetDetail(sessionId, DateTimeOffset.UtcNow) is { } detail
        ? Results.Json(detail)
        : Results.NotFound());

app.MapGet("/telemetry", () => Results.Content(TelemetryDashboard.Html, "text/html; charset=utf-8"));

app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = files,
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream"
});
app.MapFallback(async context =>
{
    if (context.Request.Path.StartsWithSegments("/telemetry"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(Path.Combine(settings.WebRoot, "index.html"));
});

Console.WriteLine($"Calculator browser host: https://0.0.0.0:{settings.Port}");
Console.WriteLine($"Web root: {settings.WebRoot}");
Console.WriteLine($"Telemetry dashboard: https://127.0.0.1:{settings.Port}/telemetry");
Console.WriteLine($"Telemetry directory: {settings.TelemetryDirectory}");
await app.RunAsync();

internal sealed record BrowserHostSettings(string WebRoot, string TelemetryDirectory, int Port)
{
    public static BrowserHostSettings Parse(string[] arguments)
    {
        if (arguments.Length != 1)
        {
            throw new ArgumentException("Usage: Calculator.BrowserHost <published-wwwroot>");
        }

        var webRoot = Path.GetFullPath(arguments[0]);
        if (!File.Exists(Path.Combine(webRoot, "index.html")))
        {
            throw new DirectoryNotFoundException($"Published browser web root not found: {webRoot}");
        }

        var telemetryDirectory = Environment.GetEnvironmentVariable("CALCULATOR_TELEMETRY_DIRECTORY");
        if (string.IsNullOrWhiteSpace(telemetryDirectory))
        {
            telemetryDirectory = Path.Combine(Path.GetTempPath(), "calcneo-browser-telemetry");
        }

        var portText = Environment.GetEnvironmentVariable("CALCULATOR_BROWSER_PORT");
        var port = string.IsNullOrWhiteSpace(portText) ? 5221 : int.Parse(portText);
        if (port is < 1 or > 65_535)
        {
            throw new ArgumentOutOfRangeException(nameof(portText), "Port must be between 1 and 65535.");
        }

        return new BrowserHostSettings(
            webRoot,
            Path.GetFullPath(telemetryDirectory),
            port);
    }
}
