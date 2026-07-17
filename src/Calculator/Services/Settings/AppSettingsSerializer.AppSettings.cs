// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.
using System.Text.Json;
using System.Text.Json.Serialization;

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

        return (JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings()).Normalize();
    }
}
