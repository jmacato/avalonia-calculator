using Avalonia.Controls;
using Avalonia.Input;

namespace MathComposer.Avalonia.Controls;

internal sealed record MathClipboardReadResult(
    string? MathMl,
    string? Latex,
    string? UnicodeMath,
    bool UsedFallback)
{
    public bool HasAny => MathMl is not null || Latex is not null || UnicodeMath is not null;
}
