// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.Services.Settings;

public sealed class InMemorySettingsStore(AppSettings? current = null) : SettingsStoreBase(current ?? new AppSettings())
{
    protected override void Persist(AppSettings settings)
    {
        _ = settings;
    }
}
