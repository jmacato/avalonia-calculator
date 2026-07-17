using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using Avalonia.VisualTree;
using SkiaSharp;

namespace FluentAvalonia.UI.Controls.Primitives;

internal enum FAProgressRingAnimatedVisualHandlerMessageType
{
    Background,
    Foreground,
    Min,
    Max,
    Value,
    Active,
    Indeterminate,
    Release
}
