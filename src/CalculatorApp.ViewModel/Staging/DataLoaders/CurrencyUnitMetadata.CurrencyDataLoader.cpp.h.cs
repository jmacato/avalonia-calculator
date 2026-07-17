// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
// #pragma once
// #include "CalcManager/UnitConverter.h"
// #include "Common/NetworkManager.h"
// #include "CurrencyHttpClient.h"
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UCM = UnitConversionManager;
using CurrencyRatioMap = System.Collections.Generic.Dictionary<string, UnitConversionManager.CurrencyRatio>;
using SelectedUnits = (string, string);

namespace CalculatorApp.ViewModel.DataLoaders
{
    public readonly record struct CurrencyUnitMetadata
    {
        public CurrencyUnitMetadata(string symbol)
        {
            Symbol = symbol;
        }

        public string Symbol { get; }
    };
}
