// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CalculatorApp.Services.Settings;

public abstract class SettingsStoreBase : ISettingsStore
{
    private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
    private AppSettings _current;
    protected SettingsStoreBase(AppSettings current)
    {
        System.ArgumentNullException.ThrowIfNull(current);
        _current = current.Normalize();
    }

    public AppSettings Current => Volatile.Read(ref _current);

    public event EventHandler? Changed;
    public void Update(Func<AppSettings, AppSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
        {
            throw new InvalidOperationException("Settings updates must remain on their owning UI thread.");
        }

        AppSettings current = Volatile.Read(ref _current);
        AppSettings next = update(current).Normalize();
        if (next == current)
        {
            return;
        }

        Persist(next);
        Volatile.Write(ref _current, next);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    protected abstract void Persist(AppSettings settings);
}
