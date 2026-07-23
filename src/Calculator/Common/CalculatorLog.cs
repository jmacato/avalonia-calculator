namespace CalculatorApp;

/// <summary>
/// Provides a host-configurable logging boundary for the shared application.
/// </summary>
public static class CalculatorLog
{
    private static Action<CalculatorLogLevel, Exception?, string, object?[]>? s_sink;

    /// <summary>
    /// Configures the host-specific log sink.
    /// </summary>
    /// <param name="sink">
    /// The sink to receive shared application events, or <see langword="null"/> to
    /// disable logging.
    /// </param>
    public static void Configure(
        Action<CalculatorLogLevel, Exception?, string, object?[]>? sink)
    {
        Volatile.Write(ref s_sink, sink);
    }

    /// <summary>
    /// Writes an informational event.
    /// </summary>
    /// <param name="messageTemplate">The structured message template.</param>
    /// <param name="values">Values referenced by the message template.</param>
    public static void Information(
        string messageTemplate,
        params object?[] values)
    {
        Write(CalculatorLogLevel.Information, null, messageTemplate, values);
    }

    /// <summary>
    /// Writes a warning event.
    /// </summary>
    /// <param name="messageTemplate">The structured message template.</param>
    /// <param name="values">Values referenced by the message template.</param>
    public static void Warning(
        string messageTemplate,
        params object?[] values)
    {
        Write(CalculatorLogLevel.Warning, null, messageTemplate, values);
    }

    /// <summary>
    /// Writes an error event.
    /// </summary>
    /// <param name="messageTemplate">The structured message template.</param>
    /// <param name="values">Values referenced by the message template.</param>
    public static void Error(
        string messageTemplate,
        params object?[] values)
    {
        Write(CalculatorLogLevel.Error, null, messageTemplate, values);
    }

    /// <summary>
    /// Writes an error event with its associated exception.
    /// </summary>
    /// <param name="exception">The exception associated with the event.</param>
    /// <param name="messageTemplate">The structured message template.</param>
    /// <param name="values">Values referenced by the message template.</param>
    public static void Error(
        Exception exception,
        string messageTemplate,
        params object?[] values)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Write(CalculatorLogLevel.Error, exception, messageTemplate, values);
    }

    private static void Write(
        CalculatorLogLevel level,
        Exception? exception,
        string messageTemplate,
        object?[] values)
    {
        ArgumentNullException.ThrowIfNull(messageTemplate);
        ArgumentNullException.ThrowIfNull(values);
        Volatile.Read(ref s_sink)?.Invoke(
            level,
            exception,
            messageTemplate,
            values);
    }
}
