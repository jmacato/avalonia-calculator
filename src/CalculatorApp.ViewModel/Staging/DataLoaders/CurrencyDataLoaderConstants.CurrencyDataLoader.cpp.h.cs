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
    public static partial class CurrencyDataLoaderConstants
    {
        public const string CacheTimestampKey = CurrencyDataLoader.CACHE_TIMESTAMP_KEY;
        public const string CacheLangcodeKey = CurrencyDataLoader.CACHE_LANGCODE_KEY;
        public const string CacheDelimiter = CurrencyDataLoader.CACHE_DELIMITER;
        public const string StaticDataFilename = CurrencyDataLoader.STATIC_DATA_FILENAME;
        public const string AllRatiosDataFilename = CurrencyDataLoader.ALL_RATIOS_DATA_FILENAME;
        public const long DayDuration = CurrencyDataLoader.DAY_DURATION;
    }
}
