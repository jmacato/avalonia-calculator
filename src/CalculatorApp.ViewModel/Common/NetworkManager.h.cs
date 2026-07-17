// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// #pragma  once
using System.Runtime.InteropServices.WindowsRuntime;

namespace CalculatorApp.ViewModel.Common;

public partial class NetworkManager
{
    public event EventHandler<NetworkBehaviorChangedEventArgs>? NetworkBehaviorChanged;
};
