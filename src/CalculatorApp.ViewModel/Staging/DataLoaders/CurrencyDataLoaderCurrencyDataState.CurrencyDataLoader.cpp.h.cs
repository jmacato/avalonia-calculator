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
    internal sealed class CurrencyDataLoaderCurrencyDataState
    {
        public static readonly CurrencyDataLoaderCurrencyDataState Empty = new CurrencyDataLoaderCurrencyDataState(new List<UCM.Unit>(), new UCM.UnitToUnitToConversionDataMap(), new Dictionary<UCM.Unit, CurrencyUnitMetadata>());
        public CurrencyDataLoaderCurrencyDataState(List<UCM.Unit> units, UCM.UnitToUnitToConversionDataMap ratios, Dictionary<UCM.Unit, CurrencyUnitMetadata> metadata)
        {
            Units = units;
            Ratios = ratios;
            Metadata = metadata;
        }

        public List<UCM.Unit> Units { get; }
        public UCM.UnitToUnitToConversionDataMap Ratios { get; }
        public Dictionary<UCM.Unit, CurrencyUnitMetadata> Metadata { get; }
    }
}
