using Avalonia.Platform;
using MathComposer.Avalonia.Layout;
using MathComposer.Avalonia.OpenType;
using MathComposer.Avalonia.Rendering;

namespace MathComposer.Avalonia;

/// <summary>
/// Owns the application-lifetime math font, layout engine, and renderer.
/// </summary>
public static class MathFontResources
{
    private static int s_initializationCount;
    private static readonly OpenTypeMathFont SharedFont = LoadFont();

    /// <summary>Gets the number of times the embedded font was parsed.</summary>
    public static int InitializationCount => Volatile.Read(ref s_initializationCount);

    /// <summary>Gets the shared parsed OpenType MATH font.</summary>
    public static OpenTypeMathFont Font { get; } = SharedFont;

    /// <summary>Gets the shared stateless layout engine.</summary>
    public static MathLayoutEngine LayoutEngine { get; } = new(SharedFont);

    /// <summary>Gets the shared stateless renderer and resolved glyph typeface.</summary>
    public static MathRenderer Renderer { get; } = new();

    /// <summary>Forces the resources to initialize at application startup.</summary>
    public static void Initialize()
    {
        _ = Font;
        _ = LayoutEngine;
        _ = Renderer;
    }

    private static OpenTypeMathFont LoadFont()
    {
        Interlocked.Increment(ref s_initializationCount);
        using Stream stream = AssetLoader.Open(
            new Uri("avares://MathComposer.Avalonia/Assets/Fonts/XCharter-Math.otf"));
        return OpenTypeMathFont.Load(stream);
    }
}
