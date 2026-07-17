using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace Calculator.BrowserHost;

internal sealed partial class TelemetryMonitor(TelemetryStore store, ILogger<TelemetryMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                foreach (var transition in store.FindStatusTransitions(DateTimeOffset.UtcNow))
                {
                    LogStatusTransition(logger, transition.SessionId, transition.RunId, string.IsNullOrEmpty(transition.PreviousStatus) ? "new" : transition.PreviousStatus, transition.Status, transition.WorkerAgeMs, transition.UiAgeMs, string.Join(", ", transition.Signals));
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Telemetry {Session} ({Run}) {Previous} -> {Status}; worker age {WorkerAge:F0} ms, UI age {UiAge:F0} ms, signals [{Signals}]")]
    private static partial void LogStatusTransition(
        ILogger logger,
        string session,
        string run,
        string previous,
        string status,
        double workerAge,
        double uiAge,
        string signals);
}
