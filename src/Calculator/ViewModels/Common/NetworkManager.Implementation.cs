// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net.NetworkInformation;
using Avalonia.Threading;

namespace CalculatorApp.ViewModel.Common;

/// <summary>
/// Portable implementation of the original WinRT NetworkManager. The port
/// intentionally reports only available/offline because metered connection
/// cost is not consistently discoverable across the supported platforms.
/// </summary>
public sealed partial class NetworkManager
{
    private static WeakReference<NetworkManager>[] s_instances = [];

    static NetworkManager()
    {
        if (!OperatingSystem.IsBrowser())
        {
            NetworkChange.NetworkAvailabilityChanged += OnNetworkStatusChange;
        }
    }

    public NetworkManager()
    {
        Register(this);
    }

    public static NetworkAccessBehavior GetNetworkAccessBehavior()
    {
        if (OperatingSystem.IsBrowser())
        {
            return NetworkAccessBehavior.Normal;
        }

        return NetworkInterface.GetIsNetworkAvailable()
            ? NetworkAccessBehavior.Normal
            : NetworkAccessBehavior.Offline;
    }

    private static void Register(NetworkManager instance)
    {
        var reference = new WeakReference<NetworkManager>(instance);
        while (true)
        {
            WeakReference<NetworkManager>[] snapshot = Volatile.Read(ref s_instances);
            var updated = new WeakReference<NetworkManager>[snapshot.Length + 1];
            snapshot.CopyTo(updated, 0);
            updated[^1] = reference;
            if (ReferenceEquals(
                    Interlocked.CompareExchange(ref s_instances, updated, snapshot),
                    snapshot))
            {
                return;
            }
        }
    }

    private static void OnNetworkStatusChange(object? sender, NetworkAvailabilityEventArgs e)
    {
        _ = sender;
        NetworkAccessBehavior behavior = e.IsAvailable
            ? NetworkAccessBehavior.Normal
            : NetworkAccessBehavior.Offline;
        WeakReference<NetworkManager>[] snapshot = Volatile.Read(ref s_instances);
        bool foundDeadReference = false;
        foreach (WeakReference<NetworkManager> reference in snapshot)
        {
            if (reference.TryGetTarget(out NetworkManager? manager))
            {
                manager.PublishNetworkBehavior(behavior);
            }
            else
            {
                foundDeadReference = true;
            }
        }

        if (foundDeadReference)
        {
            PruneDeadReferences(snapshot);
        }
    }

    private void PublishNetworkBehavior(NetworkAccessBehavior behavior)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            RaiseNetworkBehaviorChanged(behavior);
        }
        else
        {
            Dispatcher.UIThread.Post(() => RaiseNetworkBehaviorChanged(behavior));
        }
    }

    private static void PruneDeadReferences(WeakReference<NetworkManager>[] snapshot)
    {
        var liveReferences = new WeakReference<NetworkManager>[snapshot.Length];
        int liveCount = 0;
        foreach (WeakReference<NetworkManager> reference in snapshot)
        {
            if (reference.TryGetTarget(out _))
            {
                liveReferences[liveCount++] = reference;
            }
        }

        if (liveCount == snapshot.Length)
        {
            return;
        }

        Array.Resize(ref liveReferences, liveCount);
        Interlocked.CompareExchange(ref s_instances, liveReferences, snapshot);
    }
}
