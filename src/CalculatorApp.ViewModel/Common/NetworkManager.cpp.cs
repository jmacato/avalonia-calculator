// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #include  "pch.h"
// #include  "NetworkManager.h"

using System;
using CalculatorApp;
using CalculatorApp.ViewModel;
using CalculatorApp.ViewModel.Common;
using Windows.Networking.Connectivity;

namespace CalculatorApp.ViewModel.Common;

public partial class NetworkManager
{
    public NetworkManager()
    {
        NetworkInformation.NetworkStatusChanged += OnNetworkStatusChange;
    }

    ~NetworkManager()
    {
        NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChange;
    }

    public NetworkAccessBehavior GetNetworkAccessBehavior()
    {
        NetworkAccessBehavior behavior = NetworkAccessBehavior.Offline;
        ConnectionProfile connectionProfile = NetworkInformation.GetInternetConnectionProfile();
        if (connectionProfile != null)
        {
            NetworkConnectivityLevel connectivityLevel = connectionProfile.GetNetworkConnectivityLevel();
            if (connectivityLevel == NetworkConnectivityLevel.InternetAccess ||
                connectivityLevel == NetworkConnectivityLevel.ConstrainedInternetAccess)
            {
                ConnectionCost connectionCost = connectionProfile.GetConnectionCost();
                behavior = ConvertCostInfoToBehavior(connectionCost);
            }
        }

        return behavior;
    }

    public  void OnNetworkStatusChange(Object sender)
    {
        NetworkBehaviorChanged(GetNetworkAccessBehavior());
    }

// See app behavior guidelines at https://msdn.microsoft.com/en-us/library/windows/apps/xaml/jj835821(v=win.10).aspx
    public NetworkAccessBehavior ConvertCostInfoToBehavior(ConnectionCost connectionCost)
    {
        if (connectionCost.Roaming || connectionCost.OverDataLimit ||
            connectionCost.NetworkCostType == NetworkCostType.Variable
            || connectionCost.NetworkCostType == NetworkCostType.Fixed)
        {
            return NetworkAccessBehavior.OptIn;
        }

        return NetworkAccessBehavior.Normal;
    }
}
