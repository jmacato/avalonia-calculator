// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// #pragma  once
using System;

namespace CalculatorApp.ViewModel.Common;

public sealed class NetworkBehaviorChangedEventArgs : EventArgs
{
    public NetworkBehaviorChangedEventArgs(NetworkAccessBehavior behavior)
    {
        Behavior = behavior;
    }

    public NetworkAccessBehavior Behavior { get; }
}
