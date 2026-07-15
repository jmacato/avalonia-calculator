using Avalonia.Media;

namespace CSharpMath.Avalonia;

public static class Extensions
{
    public static Color ToAvaloniaColor(this System.Drawing.Color color) =>
        new(color.A, color.R, color.G, color.B);

    internal static System.Drawing.Color ToCSharpMathColor(this Color color) =>
        System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);

    public static SolidColorBrush ToSolidColorBrush(this System.Drawing.Color color) =>
        new(color.ToAvaloniaColor());
}
