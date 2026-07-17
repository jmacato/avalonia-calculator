using Avalonia;
using Avalonia.Controls;

namespace FluentAvalonia.UI.Controls;

/// <summary>
/// Represents data for use in a SettingsExpander temlate
/// </summary>
public sealed class FASettingsExpanderTemplateSettings : AvaloniaObject
{
    internal FASettingsExpanderTemplateSettings() { }

    /// <summary>
    /// Defines the <see cref="Icon"/> property
    /// </summary>
    public static readonly StyledProperty<Control?> IconProperty =
        AvaloniaProperty.Register<FASettingsExpanderTemplateSettings, Control?>(nameof(Icon));

    /// <summary>
    /// Defines the <see cref="ActionIcon"/> property
    /// </summary>
    public static readonly StyledProperty<Control?> ActionIconProperty =
        AvaloniaProperty.Register<FASettingsExpanderTemplateSettings, Control?>(nameof(ActionIcon));

    /// <summary>
    /// Defines the FAIconElement to be used for the SettingsExpander
    /// </summary>
    public Control? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Defines the FAIconElement to be used for the SettingsExpander ActionIcon
    /// </summary>
    public Control? ActionIcon
    {
        get => GetValue(ActionIconProperty);
        set => SetValue(ActionIconProperty, value);
    }
}
