using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Calculator.BrowserHost;

internal sealed class TelemetryStore
{
    private static readonly TimeSpan SessionRetention = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, TelemetryStoreSessionState> _sessions = new(StringComparer.Ordinal);
    private readonly Channel<TelemetryLogEnvelope> _logChannel = Channel.CreateBounded<TelemetryLogEnvelope>(new BoundedChannelOptions(4_096) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false });
    private long _minimumSessionStartedAtUnixMs;
    public ChannelReader<TelemetryLogEnvelope> LogEntries => _logChannel.Reader;

    public bool TryIngest(TelemetryBatch batch, string remoteAddress, out string? error)
    {
        error = ValidateAndSanitize(batch);
        if (error is not null)
        {
            return false;
        }

        long sessionStartedAt = batch.Client?.StartedAtUnixMs ?? long.MaxValue;
        if (sessionStartedAt <= Volatile.Read(ref _minimumSessionStartedAtUnixMs))
        {
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        while (true)
        {
            var session = _sessions.GetOrAdd(batch.SessionId!, static (id, state) => new TelemetryStoreSessionState(id, state.RunId!, state.RemoteAddress, state.Now), (RunId: batch.RunId, RemoteAddress: remoteAddress, Now: now));
            session.Ingest(batch, remoteAddress, now);
            if (_sessions.TryGetValue(batch.SessionId!, out TelemetryStoreSessionState? registered) && ReferenceEquals(session, registered))
            {
                break;
            }
        }

        if (sessionStartedAt <= Volatile.Read(ref _minimumSessionStartedAtUnixMs))
        {
            _sessions.TryRemove(batch.SessionId!, out _);
            return true;
        }

        _logChannel.Writer.TryWrite(new TelemetryLogEnvelope(now, remoteAddress, batch));
        return true;
    }

    public TelemetrySessionSummary[] GetSummaries(DateTimeOffset now)
    {
        PurgeExpired(now);
        return _sessions.Values.Select(session => session.GetSummary(now)).OrderByDescending(summary => summary.LastReceivedAt).ToArray();
    }

    public TelemetrySessionDetail? GetDetail(string sessionId, DateTimeOffset now)
    {
        PurgeExpired(now);
        return _sessions.TryGetValue(sessionId, out var session) ? session.GetDetail(now) : null;
    }

    public TelemetryInputTraceDocument? GetInputTrace(string sessionId, DateTimeOffset now)
    {
        PurgeExpired(now);
        return _sessions.TryGetValue(sessionId, out var session) ? session.GetInputTrace() : null;
    }

    public TelemetryStatusTransition[] FindStatusTransitions(DateTimeOffset now)
    {
        PurgeExpired(now);
        var transitions = new List<TelemetryStatusTransition>();
        foreach (var session in _sessions.Values)
        {
            if (session.TryGetStatusTransition(now, out var transition))
            {
                transitions.Add(transition);
            }
        }

        return transitions.ToArray();
    }

    public int ClearSessions()
    {
        Interlocked.Exchange(ref _minimumSessionStartedAtUnixMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        int removed = 0;
        foreach (KeyValuePair<string, TelemetryStoreSessionState> session in _sessions)
        {
            if (_sessions.TryRemove(session))
            {
                removed++;
            }
        }

        return removed;
    }

    private void PurgeExpired(DateTimeOffset now)
    {
        DateTimeOffset cutoff = now - SessionRetention;
        foreach (KeyValuePair<string, TelemetryStoreSessionState> session in _sessions)
        {
            if (session.Value.LastReceivedAt < cutoff)
            {
                _sessions.TryRemove(session);
            }
        }
    }

    private static string? ValidateAndSanitize(TelemetryBatch batch)
    {
        if (batch.Version != 1)
        {
            return "Unsupported telemetry version.";
        }

        if (!IsSessionId(batch.SessionId))
        {
            return "Invalid session id.";
        }

        if (batch.WorkerSequence < 0 || batch.WorkerUploadFailures < 0 || batch.SkippedUploads < 0)
        {
            return "Invalid counter value.";
        }

        batch.RunId = Truncate(batch.RunId, 96);
        batch.Source = Truncate(batch.Source, 24);
        if (batch.Source is not ("worker" or "ui-beacon"))
        {
            return "Invalid telemetry source.";
        }

        if (batch.Client is { } client)
        {
            client.UserAgent = Truncate(client.UserAgent, 384);
            client.Platform = Truncate(client.Platform, 96);
            client.Language = Truncate(client.Language, 48);
            client.Page = Truncate(client.Page, 384);
        }

        if (batch.Ui is { } ui)
        {
            ui.BootStage = Truncate(ui.BootStage, 80);
            ui.Visibility = Truncate(ui.Visibility, 32);
            ui.ActiveElement = Truncate(ui.ActiveElement, 192);
        }

        if (batch.Events is { Count: > 64 })
        {
            return "Too many events in one batch.";
        }

        if (batch.Events is not null)
        {
            foreach (var item in batch.Events)
            {
                item.Kind = Truncate(item.Kind, 64);
                item.Detail = Truncate(item.Detail, 768);
            }
        }

        if (batch.InputTraceValues is { Length: > 0 } inputTraceValues)
        {
            if (batch.InputTraceStride != TelemetryInputTraceDocument.Stride ||
                inputTraceValues.Length > 4_096 ||
                inputTraceValues.Length % batch.InputTraceStride != 0 ||
                batch.InputTraceStartSequence <= 0 ||
                batch.InputTraceDropped < 0)
            {
                return "Invalid input trace.";
            }

            foreach (double value in inputTraceValues)
            {
                if (!double.IsFinite(value))
                {
                    return "Input trace contains a non-finite value.";
                }
            }
        }
        else if (batch.InputTraceStartSequence != 0 || batch.InputTraceStride != 0 || batch.InputTraceDropped < 0)
        {
            return "Invalid empty input trace.";
        }

        return null;
    }

    private static bool IsSessionId(string? value)
    {
        if (value is null || value.Length is < 8 or > 80)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static string Truncate(string? value, int maximumLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maximumLength ? value : value[..maximumLength];
    }
}
