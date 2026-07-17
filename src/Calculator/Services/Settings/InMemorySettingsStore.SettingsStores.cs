// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CalculatorApp.Services.Settings;

public sealed class InMemorySettingsStore : SettingsStoreBase
{
    public InMemorySettingsStore(AppSettings? current = null) : base(current ?? new AppSettings())
    {
    }

    protected override void Persist(AppSettings settings)
    {
        _ = settings;
    }
}
