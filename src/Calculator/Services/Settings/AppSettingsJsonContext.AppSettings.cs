// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CalculatorApp.Services.Settings;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
public sealed partial class AppSettingsJsonContext : JsonSerializerContext;
