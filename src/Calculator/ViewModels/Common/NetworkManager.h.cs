// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #pragma  once

namespace CalculatorApp.ViewModel.Common;

public enum NetworkAccessBehavior
{
    Normal = 0,
    OptIn = 1,
    Offline = 2
};

public
    delegate void NetworkBehaviorChangedHandler(NetworkAccessBehavior behavior);

public partial class NetworkManager
{
    public event NetworkBehaviorChangedHandler? NetworkBehaviorChanged;

    protected void RaiseNetworkBehaviorChanged(NetworkAccessBehavior behavior) =>
        NetworkBehaviorChanged?.Invoke(behavior);
};
