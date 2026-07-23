using System.Text.Json;

namespace Calculator.BrowserHost;

internal sealed partial class TelemetryLogWriter(TelemetryStore store, BrowserHostSettings settings, ILogger<TelemetryLogWriter> logger) : BackgroundService
{
    public string LogPath { get; } = Path.Combine(settings.TelemetryDirectory, $"browser-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.ndjson");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(settings.TelemetryDirectory);
        var stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var configuredStream = stream.ConfigureAwait(false);
        var writer = new StreamWriter(stream);
        await using var configuredWriter = writer.ConfigureAwait(false);
        try
        {
            await foreach (var entry in store.LogEntries.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(
                    entry,
                    TelemetryJsonContext.Default.TelemetryLogEnvelope)).ConfigureAwait(false);
                await writer.FlushAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (IOException exception)
        {
            LogFailure(logger, exception);
        }
        catch (JsonException exception)
        {
            LogFailure(logger, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            LogFailure(logger, exception);
        }
    }

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Telemetry log writer failed.")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
