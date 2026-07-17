namespace FluentAvalonia.UI.Input;

/// <summary>
/// Provides event data for the ExecuteRequested event.
/// </summary>
public class FAExecuteRequestedEventArgs : EventArgs
{
    internal FAExecuteRequestedEventArgs(object? parameter)
    {
        Parameter = parameter;
    }

    /// <summary>
    /// Gets the command parameter passed into the Execute method that raised this event.
    /// </summary>
    public object? Parameter { get; }
}
