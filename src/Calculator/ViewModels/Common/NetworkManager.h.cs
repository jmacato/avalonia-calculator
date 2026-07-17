// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// #pragma  once
namespace CalculatorApp.ViewModel.Common;

public sealed partial class NetworkManager
{
    public event EventHandler<NetworkBehaviorChangedEventArgs>? NetworkBehaviorChanged;
    private void RaiseNetworkBehaviorChanged(NetworkAccessBehavior behavior) =>
        NetworkBehaviorChanged?.Invoke(this, new NetworkBehaviorChangedEventArgs(behavior));
};
