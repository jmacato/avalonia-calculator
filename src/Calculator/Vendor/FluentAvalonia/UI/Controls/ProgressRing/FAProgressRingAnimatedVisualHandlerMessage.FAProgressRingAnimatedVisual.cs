using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using Avalonia.VisualTree;
using SkiaSharp;

namespace FluentAvalonia.UI.Controls.Primitives;

internal sealed class FAProgressRingAnimatedVisualHandlerMessage
{
    public FAProgressRingAnimatedVisualHandlerMessage(FAProgressRingAnimatedVisualHandlerMessageType type, object? data = null)
    {
        MessageType = type;
        Data = data;
    }

    public FAProgressRingAnimatedVisualHandlerMessageType MessageType { get; }
    public object? Data { get; }
}
