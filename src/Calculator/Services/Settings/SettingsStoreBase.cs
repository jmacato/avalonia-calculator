// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.Services.Settings;

public abstract class SettingsStoreBase : ISettingsStore
{
    private int _ownerThreadState = Environment.CurrentManagedThreadId;
    private AppSettings _current;

    protected SettingsStoreBase(AppSettings current)
    {
        ArgumentNullException.ThrowIfNull(current);
        _current = current.Normalize();
    }

    public AppSettings Current => Volatile.Read(ref _current);

    public event EventHandler? Changed;

    public void BindToCurrentThread()
    {
        int currentThreadId = Environment.CurrentManagedThreadId;
        int boundState = EncodeBoundOwner(currentThreadId);

        while (true)
        {
            int state = Volatile.Read(ref _ownerThreadState);
            if (state < 0)
            {
                VerifyOwner(state, currentThreadId);
                return;
            }

            if (Interlocked.CompareExchange(
                    ref _ownerThreadState,
                    boundState,
                    state) == state)
            {
                return;
            }
        }
    }

    public void Update(Func<AppSettings, AppSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        VerifyAndSealOwner();

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

    private void VerifyAndSealOwner()
    {
        int currentThreadId = Environment.CurrentManagedThreadId;
        while (true)
        {
            int state = Volatile.Read(ref _ownerThreadState);
            VerifyOwner(state, currentThreadId);
            if (state < 0 || Interlocked.CompareExchange(
                    ref _ownerThreadState,
                    EncodeBoundOwner(currentThreadId),
                    state) == state)
            {
                return;
            }
        }
    }

    private static void VerifyOwner(int state, int currentThreadId)
    {
        int ownerThreadId = state < 0 ? DecodeBoundOwner(state) : state;
        if (currentThreadId != ownerThreadId)
        {
            throw new InvalidOperationException(
                $"Settings updates must remain on managed thread {ownerThreadId}; " +
                $"the current managed thread is {currentThreadId}.");
        }
    }

    private static int EncodeBoundOwner(int threadId)
    {
        return ~threadId;
    }

    private static int DecodeBoundOwner(int state)
    {
        return ~state;
    }
}
