namespace FluentAvalonia.UI.Controls.Primitives;

internal sealed class FAProgressRingAnimatedVisualHandlerMessage(
    FAProgressRingAnimatedVisualHandlerMessageType type,
    object? data = null)
{
    public FAProgressRingAnimatedVisualHandlerMessageType MessageType { get; } = type;
    public object? Data { get; } = data;
}
