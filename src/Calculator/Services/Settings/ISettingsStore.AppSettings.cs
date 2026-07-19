// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CalculatorApp.Services.Settings;

public interface ISettingsStore
{
    AppSettings Current { get; }

    event EventHandler? Changed;

    void BindToCurrentThread();

    void Update(Func<AppSettings, AppSettings> update);
}
