// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.
using System.Text.Json;

namespace CalculatorApp.Services.Settings;

public static class AppSettingsSerializer
{
    public static string Serialize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return JsonSerializer.Serialize(settings.Normalize(), AppSettingsJsonContext.Default.AppSettings);
    }

    public static AppSettings Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppSettings();
        }

        AppSettings settings = JsonSerializer.Deserialize(
            json,
            AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();

        if (!ContainsGraphThemeMatchApp(json))
        {
            settings = settings with { GraphThemeMatchApp = true };
        }

        return settings.Normalize();
    }

    private static bool ContainsGraphThemeMatchApp(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("graphThemeMatchApp", out _);
    }
}
