using Avalonia.Controls;
using Avalonia.Input;

namespace MathComposer.Avalonia.Controls;

internal sealed record MathClipboardPayload(string MathMl, string Latex, string UnicodeMath);
