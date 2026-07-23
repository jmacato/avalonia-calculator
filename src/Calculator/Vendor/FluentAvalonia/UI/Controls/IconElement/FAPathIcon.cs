using Avalonia.Controls;
using Avalonia.Media;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// WinUI-compatible name for Avalonia's vector path icon.
/// </summary>
public sealed class FAPathIcon : PathIcon
{
    protected override Type StyleKeyOverride => typeof(PathIcon);

    public static bool IsDataValid(string data, out Geometry? geometry)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (string.IsNullOrWhiteSpace(data))
        {
            geometry = null;
            return false;
        }

        try
        {
            geometry = StreamGeometry.Parse(data);
            return true;
        }
        catch (InvalidDataException)
        {
            geometry = null;
            return false;
        }
        catch (NotSupportedException)
        {
            geometry = null;
            return false;
        }
        catch (FormatException)
        {
            geometry = null;
            return false;
        }
    }
}
