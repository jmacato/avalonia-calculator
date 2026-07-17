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
    public partial class CurrencyDataLoader : UCM.IConverterDataLoader, UCM.ICurrencyConverterDataLoader
    {
        // // public:
        // CurrencyDataLoader(const wchar_t* overrideLanguage = null);
        // ~CurrencyDataLoader();
        //
        // bool LoadFinished();
        // bool LoadedFromCache();
        // bool LoadedFromWeb();
        //
        // // IConverterDataLoader
        // void LoadData() override;
        // List<UCM.Category> GetOrderedCategories() override;
        // List<UCM.Unit> GetOrderedUnits(const UCM.Category category) override;
        // Dictionary<UCM.Unit, UCM.ConversionData, UCM.UnitHash> LoadOrderedRatios(const UCM.Unit unit) override;
        // bool SupportsCategory(const UnitConversionManager.Category target) override;
        // // IConverterDataLoader
        // // ICurrencyConverterDataLoader
        // void SetViewModelCallback(const UCM.IViewModelCurrencyCallback callback) override;
        // (string, string) GetCurrencySymbols(const UCM.Unit unit1, const UCM.Unit unit2) override;
        // (string, string)
        // GetCurrencyRatioEquality(   const UnitConversionManager.Unit unit1,    const UnitConversionManager.Unit unit2) override;
        // string GetCurrencyTimestamp() override;
        // static double RoundCurrencyRatio(double ratio);
        //
        // async Task<bool> TryLoadDataFromCacheAsync() override;
        // async Task<bool> TryLoadDataFromWebAsync() override;
        // async Task<bool> TryLoadDataFromWebOverrideAsync() override;
        // // ICurrencyConverterDataLoader
        //
        // void OnNetworkBehaviorChanged(CalculatorApp.ViewModel.Common.NetworkAccessBehavior newBehavior);
        // private:
        // void ResetLoadStatus();
        // void NotifyDataLoadFinished(bool didLoad);
        //
        // async Task<bool> TryFinishLoadFromCacheAsync();
        //
        // bool TryParseWebResponses(
        //        string  staticDataJson,
        //        string  allRatiosJson,
        //      ref  List<UCM.CurrencyStaticData> staticData,
        //      ref  CurrencyRatioMap allRatiosData);
        // bool TryParseStaticData(   string  rawJson,  ref  List<UCM.CurrencyStaticData> staticData);
        // bool TryParseAllRatiosData(   string  rawJson,  ref  CurrencyRatioMap allRatiosData);
        // concurrency.task<void> FinalizeUnits(   const List<UCM.CurrencyStaticData> staticData,    const CurrencyRatioMap ratioMap);
        // void GuaranteeSelectedUnits();
        //
        // void SaveLangCodeAndTimestamp();
        // void UpdateDisplayedTimestamp();
        //
        // void RegisterForNetworkBehaviorChanges();
        // void UnregisterForNetworkBehaviorChanges();
        //
        // concurrency.task<SelectedUnits> GetDefaultFromToCurrency();
        // bool TryGetLastUsedCurrenciesFromLocalSettings(out string const fromCurrency, out string const toCurrency);
        // void SaveSelectedUnitsToLocalSettings(   const SelectedUnits selectedUnits);
        // private:
        string m_responseLanguage;
        CurrencyHttpClient m_client = new();
        bool m_isRtlLanguage;
        CurrencyDataLoaderCurrencyDataState m_currencyData = CurrencyDataLoaderCurrencyDataState.Empty;
        UCM.IViewModelCurrencyCallback? m_vmCallback;
        Windows.Globalization.NumberFormatting.DecimalFormatter m_ratioFormatter;
        string m_ratioFormat;
        DateTime m_cacheTimestamp;
        string m_timestampFormat;
        CurrencyLoadStatus m_loadStatus;
        CalculatorApp.ViewModel.Common.NetworkManager m_networkManager;
        CalculatorApp.ViewModel.Common.NetworkAccessBehavior m_networkAccessBehavior;
        //Windows.Foundation.EventRegistrationToken m_networkBehaviorToken;
        bool m_meteredOverrideSet;
    };
}
