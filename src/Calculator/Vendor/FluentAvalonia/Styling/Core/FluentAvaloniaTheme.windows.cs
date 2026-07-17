using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;

namespace FluentAvalonia.Styling;

public sealed partial class FluentAvaloniaTheme
{
    private ThemeVariant? ResolveWindowsSystemSettings(IPlatformSettings platformSettings)
    {
        ThemeVariant? theme = null;
        if (PreferSystemTheme)
        {
            theme = GetThemeFromIPlatformSettings(platformSettings);
        }

        if (CustomAccentColor != null)
        {
            LoadCustomAccentColor();
        }
        else if (PreferUserAccentColor)
        {
            TryLoadPlatformAccentColor(platformSettings);
        }
        else
        {
            LoadDefaultAccentColor();
        }

        if (UseSystemFontOnWindows)
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                AddOrUpdateSystemResource("ContentControlThemeFontFamily", new FontFamily("Segoe UI Variable"));
            }
            else
            {
                AddOrUpdateSystemResource("ContentControlThemeFontFamily", new FontFamily("Segoe UI"));
            }
        }

        return theme;
    }

    private void TryLoadHighContrastThemeColors()
    {
        if (!OperatingSystem.IsWindows() || Resources.MergedDictionaries.Count == 0 ||
            Resources.MergedDictionaries[0] is not ResourceDictionary rootDictionary ||
            rootDictionary.ThemeDictionaries[HighContrastTheme] is not ResourceDictionary highContrastResources)
        {
            return;
        }

        static Color ConvertColor(System.Drawing.Color color)
        {
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        highContrastResources["SystemColorWindowTextColor"] = ConvertColor(System.Drawing.SystemColors.WindowText);
        highContrastResources["SystemColorGrayTextColor"] = ConvertColor(System.Drawing.SystemColors.GrayText);
        highContrastResources["SystemColorButtonFaceColor"] = ConvertColor(System.Drawing.SystemColors.ButtonFace);
        highContrastResources["SystemColorWindowColor"] = ConvertColor(System.Drawing.SystemColors.Window);
        highContrastResources["SystemColorButtonTextColor"] = ConvertColor(System.Drawing.SystemColors.ControlText);
        highContrastResources["SystemColorHighlightColor"] = ConvertColor(System.Drawing.SystemColors.Highlight);
        highContrastResources["SystemColorHighlightTextColor"] = ConvertColor(System.Drawing.SystemColors.HighlightText);
        highContrastResources["SystemColorHotlightColor"] = ConvertColor(System.Drawing.SystemColors.HotTrack);
    }

}
