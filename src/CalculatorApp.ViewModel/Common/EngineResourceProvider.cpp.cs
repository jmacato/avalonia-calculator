// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #include  "pch.h"
// #include  "EngineResourceProvider.h"
// #include  "Common/LocalizationSettings.h"

using System;
using CalculatorApp.ViewModel.Common;
using Windows.ApplicationModel.Resources;
using CalculationManager;

namespace CalculatorApp.ViewModel.Common;

public partial class EngineResourceProvider : IResourceProvider
{
    public EngineResourceProvider()
    {
        m_resLoader = ResourceLoader.GetForViewIndependentUse("CEngineStrings");
    }

    public string GetCEngineString(string id)
    {
        LocalizationSettings localizationSettings = LocalizationSettings.Instance;

        if (id == ("sDecima") || id == ("sDecimal"))
        {
            return localizationSettings.GetDecimalSeparatorStr();
        }

        if (id == ("sThousand"))
        {
            return localizationSettings.GetNumberGroupingSeparatorStr();
        }

        if (id == ("sGrouping"))
        {
            // The following groupings are the onces that CalcEngine supports.
            //   0;0             0x000          - no grouping
            //   3;0             0x003          - group every 3 digits
            //   3;2;0           0x023          - group 1st 3 and then every 2 digits
            //   4;0             0x004          - group every 4 digits
            //   5;3;2;0         0x235          - group 5, then 3, then every 2
            string numberGroupingString = localizationSettings.NumberGrouping;
            return numberGroupingString;
        }

        // StringReference idRef(id.data
        // (), id.length());
        return m_resLoader.GetString(id);
    }
}
