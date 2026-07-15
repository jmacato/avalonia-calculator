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
public sealed partial class NetworkManager : IDisposable
{
    private bool _disposed;

    public NetworkManager()
    {
        if (!OperatingSystem.IsBrowser())
        {
            NetworkChange.NetworkAvailabilityChanged += OnNetworkStatusChange;
        }
    }

    ~NetworkManager() => Dispose(disposing: false);

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

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void OnNetworkStatusChange(object? sender, NetworkAvailabilityEventArgs e)
    {
        _ = sender;
        NetworkAccessBehavior behavior = e.IsAvailable
            ? NetworkAccessBehavior.Normal
            : NetworkAccessBehavior.Offline;
        if (Dispatcher.UIThread.CheckAccess())
        {
            RaiseNetworkBehaviorChanged(behavior);
        }
        else
        {
            Dispatcher.UIThread.Post(() => RaiseNetworkBehaviorChanged(behavior));
        }
    }

    private void Dispose(bool disposing)
    {
        _ = disposing;
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (!OperatingSystem.IsBrowser())
        {
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkStatusChange;
        }
    }
}
