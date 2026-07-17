// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalculatorApp.ViewModel.Common;

public sealed class NetworkBehaviorChangedEventArgs(NetworkAccessBehavior behavior) : EventArgs
{
    public NetworkAccessBehavior Behavior { get; } = behavior;
}
