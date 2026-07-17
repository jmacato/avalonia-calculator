using Calculator.BrowserHost;
using System.Globalization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

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
        var port = string.IsNullOrWhiteSpace(portText) ? 5221 : int.Parse(portText, NumberStyles.None, CultureInfo.InvariantCulture);
        if (port is < 1 or > 65_535)
        {
            throw new InvalidOperationException("CALCULATOR_BROWSER_PORT must be between 1 and 65535.");
        }

        return new BrowserHostSettings(webRoot, Path.GetFullPath(telemetryDirectory), port);
    }
}
