// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.Services.Settings;

public interface ISettingsStore
{
    AppSettings Current { get; }

    event EventHandler? Changed;

    void BindToCurrentThread();

    void Update(Func<AppSettings, AppSettings> update);
}
