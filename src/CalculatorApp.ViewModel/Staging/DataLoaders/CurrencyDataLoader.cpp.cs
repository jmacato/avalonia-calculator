// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #include "pch.h"
// #include <optional>

// #include "CurrencyDataLoader.cpp.h"
// #include "Common/AppResourceProvider.h"
// #include "Common/LocalizationStringUtil.h"
// #include "Common/LocalizationService.h"
// #include "Common/LocalizationSettings.h"
// #include "Common/TraceLogger.h"
// #include "UnitConverterDataConstants.h"

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using  CalculatorApp;
using  CalculatorApp.ViewModel.Common;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UCM = UnitConversionManager;
using CurrencyRatioMap = System.Collections.Generic.Dictionary<string, UnitConversionManager.CurrencyRatio>;
using SelectedUnits = (string first, string second);
using  CalculatorApp.ViewModel.DataLoaders;
using  CalculatorApp.ViewModel;
using  UnitConversionManager;
using  Windows.ApplicationModel.Resources.Core;
using  Windows.Data.Json;
using  Windows.Foundation;
using  Windows.Foundation.Collections;
using  Windows.Globalization.DateTimeFormatting;
using  Windows.Globalization.NumberFormatting;
using  Windows.Storage;
using  Windows.System.UserProfile;
using  Windows.UI.Core;
using  Windows.Web.Http;
using Microsoft.VisualBasic.CompilerServices;

namespace CalculatorApp. ViewModel.DataLoaders;

public partial class CurrencyDataLoader
{
    internal const string CURRENCY_UNIT_FROM_KEY = "CURRENCY_UNIT_FROM_KEY";
    internal const string CURRENCY_UNIT_TO_KEY = "CURRENCY_UNIT_TO_KEY";

    // Calculate number of 100-nanosecond intervals-per-day
    // (1 interval/100 nanosecond)(100 nanosecond/1e-7 s)(60 s/1 min)(60 min/1 hr)(24 hr/1 day) = (interval/day)
    internal const long DAY_DURATION = 1L * 60 * 60 * 24 * 10000000;
    internal const long WEEK_DURATION = DAY_DURATION * 7;

    internal const int FORMATTER_RATE_FRACTION_PADDING = 2;
    internal const int FORMATTER_RATE_MIN_DECIMALS = 4;
    internal const int FORMATTER_RATE_MIN_SIGNIFICANT_DECIMALS = 4;

    internal const string CACHE_TIMESTAMP_KEY = "CURRENCY_CONVERTER_TIMESTAMP";
    internal const string CACHE_LANGCODE_KEY = "CURRENCY_CONVERTER_LANGCODE";
    internal const string CACHE_DELIMITER = "%";

    internal const string STATIC_DATA_FILENAME = "CURRENCY_CONVERTER_STATIC_DATA.txt";
    // array<string, 5> STATIC_DATA_PROPERTIES = { string{ "CountryCode", 11 },
    //                                                             string{ "CountryName", 11 },
    //                                                             string{ "CurrencyCode", 12 },
    //                                                             string{ "CurrencyName", 12 },
    //                                                             string{ "CurrencySymbo", 14 } };

    private static readonly KeyValuePair<string, int>[] STATIC_DATA_PROPERTIES = {
        new KeyValuePair<string, int>("CountryCode", 11),
        new KeyValuePair<string, int>("CountryName", 11),
        new KeyValuePair<string, int>("CurrencyCode", 12),
        new KeyValuePair<string, int>("CurrencyName", 12),
        new KeyValuePair<string, int>("CurrencySymbol", 14)
    };

    private static readonly KeyValuePair<string, int>[] ALL_RATIOS_DATA_PROPERTIES = {
        new KeyValuePair<string, int>(RATIO_KEY, 2),
        new KeyValuePair<string, int>(CURRENCY_CODE_KEY, 2)
    };

    internal const string ALL_RATIOS_DATA_FILENAME = "CURRENCY_CONVERTER_ALL_RATIOS_DATA.txt";
    internal const string RATIO_KEY = "Rt";
    internal const string CURRENCY_CODE_KEY = "An";
    // array<string, 2> ALL_RATIOS_DATA_PROPERTIES = { string{ RATIO_KEY, 2 }, string{ CURRENCY_CODE_KEY, 2 } };

    internal const string DEFAULT_FROM_TO_CURRENCY_FILE_URI = "ms-appx:///DataLoaders/DefaultFromToCurrency.json";
    internal const string FROM_KEY = "from";
    internal const string TO_KEY = "to";

    // Fallback default values.
    internal static string DEFAULT_FROM_CURRENCY = LocalizationServiceProperties.DefaultCurrencyCode;
    internal const string DEFAULT_TO_CURRENCY = "EUR";

    // ParseLanguageCode returns language code in form of `lang-region`
    // TODO: unit testing.
    string? ParseLanguageCode(string bcp47)
    {
        // the IETF BCP 47 language tag syntax is: language[-script][-region]...
        // Handle null or empty input string
        if (string.IsNullOrEmpty(bcp47))
        {
            return null;
        }

        var segments = new List<string>();
        var currentSegment = new StringBuilder();

        // Iterate through the input string characters
        foreach (char ch in bcp47)
        {
            if (char.IsLetter(ch))
            {
                currentSegment.Append(ch); // Append letter to current segment
            }
            else if (ch == '-')
            {
                // Invalid format: empty segment (e.g., leading "-" or "--")
                if (currentSegment.Length == 0)
                {
                    return null;
                }

                // Stop processing if we already have 3 segments collected
                // (Mimics the C++ loop condition `segments.size() < 3`)
                if (segments.Count >= 3)
                {
                    break; // Stop processing further characters
                }

                segments.Add(currentSegment.ToString());
                currentSegment.Clear(); // Reset for the next segment
            }
            else
            {
                // Invalid character found
                return null;
            }
        }

        // Add the last segment if it's not empty AND we haven't hit the segment limit
        if (currentSegment.Length > 0 && segments.Count < 3)
        {
            segments.Add(currentSegment.ToString());
        }
        // Note: If currentSegment has content but segments.Count is already 3 (e.g., "en-GB-xxx"),
        // the C++ code ignores the final 'cur', and so does this C# code by not adding it here.

        // Process the collected segments
        switch (segments.Count)
        {
            case 1:
                // The segment must contain only letters due to loop checks
                return segments[0];
            case 2:
                // If the second segment looks like a 2-char region code
                if (segments[1].Length == 2)
                {
                    return segments[0] + "-" + segments[1]; // e.g., "en-US"
                }
                else // Otherwise, return only the language part
                {
                    return segments[0]; // e.g., "en" from "en-Latn"
                }
            case 3:
                // If the second segment looks like a 2-char region code
                if (segments[1].Length == 2)
                {
                    return segments[0] + "-" + segments[1]; // e.g., "en-GB" from "en-GB-oed"
                }
                // Else if the second is NOT 2-char, but the third IS 2-char (like a region after a script)
                else if (segments[2].Length == 2)
                {
                    return segments[0] + "-" + segments[2]; // e.g., "zh-CN" from "zh-Hans-CN"
                }
                else // Otherwise, return only the language part
                {
                    return segments[0]; // e.g., "az" from "az-Latn-AZ" (if Latn wasn't 2 chars)
                }
            default:
                // Handles 0 segments (e.g., input was just "-" or invalid chars)
                return null; // No valid parse according to the rules
        }
    }

    public CurrencyDataLoader(string forcedResponseLanguage = "en-US")

    {
        ;
        m_loadStatus = (CurrencyLoadStatus.NotLoaded);
        m_responseLanguage = ("en-US");
        m_ratioFormat = ("");
        m_timestampFormat = ("");
        m_networkManager = (new NetworkManager());
        m_meteredOverrideSet = (false);

        if (forcedResponseLanguage != null)
        {
            Debug.Assert((forcedResponseLanguage).Length > 0, "forcedResponseLanguage shall not be empty.");
            m_responseLanguage = (forcedResponseLanguage);
        }
        else if (GlobalizationPreferences.Languages.Count > 0)
        {
            var lang = ParseLanguageCode(GlobalizationPreferences.Languages[0]);
            if (!string.IsNullOrWhiteSpace(lang))
            {
                m_responseLanguage = lang;
            }
        }

        m_client.Initialize(LocalizationServiceProperties.DefaultCurrencyCode, m_responseLanguage);
        var localizationService = LocalizationService.GetInstance();
        if (CoreWindow.GetForCurrentThread() != null)
        {
            // Must have a CoreWindow to access the resource context.
            m_isRtlLanguage = localizationService.IsRtlLayout();
        }

        m_ratioFormatter = localizationService.GetRegionalSettingsAwareDecimalFormatter();
        m_ratioFormatter.IsGrouped = true;
        m_ratioFormatter.IsDecimalPointAlwaysDisplayed = true;
        m_ratioFormatter.FractionDigits = FORMATTER_RATE_FRACTION_PADDING;

        m_ratioFormat = AppResourceProvider.GetInstance().GetResourceString("CurrencyFromToRatioFormat");
        m_timestampFormat = AppResourceProvider.GetInstance().GetResourceString("CurrencyTimestampFormat");
    }

    ~CurrencyDataLoader()
    {
        UnregisterForNetworkBehaviorChanges();
    }

    void UnregisterForNetworkBehaviorChanges()
    {
        m_networkManager.NetworkBehaviorChanged -= OnNetworkBehaviorChanged;
    }

    void RegisterForNetworkBehaviorChanges()
    {
        UnregisterForNetworkBehaviorChanges();

        m_networkManager.NetworkBehaviorChanged += this.OnNetworkBehaviorChanged;

        OnNetworkBehaviorChanged(m_networkManager.GetNetworkAccessBehavior());
    }

    void OnNetworkBehaviorChanged(NetworkAccessBehavior newBehavior)
    {
        m_networkAccessBehavior = newBehavior;
        if (m_vmCallback != null)
        {
            m_vmCallback.NetworkBehaviorChanged((int)(m_networkAccessBehavior));
        }
    }

    bool LoadFinished()
    {
        return m_loadStatus != CurrencyLoadStatus.NotLoaded;
    }

    bool LoadedFromCache()
    {
        return m_loadStatus == CurrencyLoadStatus.LoadedFromCache;
    }

    bool LoadedFromWeb()
    {
        return m_loadStatus == CurrencyLoadStatus.LoadedFromWeb;
    }

    void ResetLoadStatus()
    {
        m_loadStatus = CurrencyLoadStatus.NotLoaded;
    }

// #pragma optimize("", off) // Optimization disabled for DevDiv 393321 compatibility

    public async void LoadData() // Changed return type to void as original didn't use the task
    {
        if (!LoadFinished())
        {
            RegisterForNetworkBehaviorChanges();

            // Use Task.Run if the initial part of the lambda needs background thread,
            // but WinRT async calls handle threading. Direct call is likely fine.
            bool didLoad = false;
            try
            {
                // Simpler async flow in C#
                didLoad = await TryLoadDataFromCacheAsync();
                if (!didLoad)
                {
                    didLoad = await TryLoadDataFromWebAsync();
                }
            }
            catch (Exception ex)
            {
                TraceLogger.GetInstance().LogStandardException(ViewMode.Currency, "LoadData", ex);
                didLoad = false;
            }
            finally // Ensure UI updates happen regardless of exceptions in loading
            {
                // Ensure UI updates are marshalled back to the UI thread if needed.
                // Assuming UpdateDisplayedTimestamp and NotifyDataLoadFinished handle threading internally or are safe.
                UpdateDisplayedTimestamp();
                NotifyDataLoadFinished(didLoad);
            }
        }
    }

// #pragma optimize("", on)

    public List<UCM.Category> GetOrderedCategories()
    {
        // This function should not be called
        // The model will use the categories from UnitConverterDataLoader
        return new List<UCM.Category>();
    }

    public List<UCM.Unit> GetOrderedUnits(UCM.Category category)
    {
        lock (m_currencyUnitsMutex)
        {
            return m_currencyUnits;
        }
    }

    public Dictionary<UCM.Unit, UCM.ConversionData> LoadOrderedRatios(UCM.Unit unit)
    {
        lock (m_currencyUnitsMutex)
        {
            return m_currencyRatioMap[unit];
        }
    }

    public bool SupportsCategory(UCM.Category target)
    {
        int currencyId = NavCategoryStates.Serialize(ViewMode.Currency);
        return target.id == currencyId;
    }

    public void SetViewModelCallback(UCM.IViewModelCurrencyCallback callback)
    {
        m_vmCallback = callback;
        OnNetworkBehaviorChanged(m_networkAccessBehavior);
    }

    public (string, string) GetCurrencySymbols(UCM.Unit unit1, UCM.Unit unit2)
    {
        lock (m_currencyUnitsMutex) ;

        string symbol1 = "";
        string symbol2 = "";

        // var itr1 = m_currencyMetadata.Try(unit1);
        // var itr2 = m_currencyMetadata.find(unit2);
        if (m_currencyMetadata.TryGetValue(unit1, out var itr1)
            && m_currencyMetadata.TryGetValue(unit2, out var itr2))
        {
            symbol1 = (itr1).symbol;
            symbol2 = (itr2).symbol;
        }

        return (symbol1, symbol2);
    }

    double RoundCurrencyRatio(double ratio)
    {
        // Compute how many decimals we need to display two meaningful digits at minimum
        // For example: 0.00000000342334 . 0.000000003423, 0.000212 . 0.000212
        int numberDecimals = FORMATTER_RATE_MIN_DECIMALS;
        if (ratio < 1)
        {
            numberDecimals = Math.Max(FORMATTER_RATE_MIN_DECIMALS,
                (int)(-Math.Log10(ratio)) + FORMATTER_RATE_MIN_SIGNIFICANT_DECIMALS);
        }

        ulong scale = (ulong)Math.Pow(10.0, numberDecimals);

        return (double)(Math.Round(ratio * scale) / scale);
    }

    public (string, string) GetCurrencyRatioEquality(UCM.Unit unit1, UCM.Unit unit2)
    {
        try
        {
            // var  = m_currencyRatioMap.find(unit1);
            if (m_currencyRatioMap.TryGetValue(unit1, out var iter)) //.end())
            {
                Dictionary<UCM.Unit, UCM.ConversionData> ratioMap = iter;

                if (ratioMap.TryGetValue(unit2, out var iter2))
                {
                    double ratio = (iter2).ratio;
                    double rounded = RoundCurrencyRatio(ratio);

                    var digit = LocalizationSettings.GetInstance().GetDigitSymbolFromEnUsDigit('1');
                    var digitSymbol = new String(digit, 1);
                    var roundedFormat = m_ratioFormatter.Format(rounded);

                    var ratioString = LocalizationStringUtil.GetLocalizedString(
                        m_ratioFormat, digitSymbol, (unit1.abbreviation), roundedFormat, (unit2.abbreviation));

                    var accessibleRatioString = LocalizationStringUtil.GetLocalizedString(
                        m_ratioFormat, digitSymbol, (unit1.accessibleName), roundedFormat, (unit2.accessibleName));

                    return (ratioString, accessibleRatioString);
                }
            }
        }
        catch
        {
        }

        return ("", "");
    }

// #pragma optimize("", off) // Optimization disabled for DevDiv 393321 compatibility
    public async Task<bool> TryLoadDataFromCacheAsync()
    {
        try
        {
            ResetLoadStatus();

            var localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings == null ||
                !localSettings.Values.ContainsKey(CurrencyDataLoaderConstants.CacheTimestampKey))
            {
                return false;
            }

            bool loadComplete = false;
            m_cacheTimestamp = (DateTime)(localSettings.Values[CurrencyDataLoaderConstants.CacheTimestampKey]);
            if (Utilities.IsDateTimeOlderThan(m_cacheTimestamp, DAY_DURATION) && m_networkAccessBehavior == NetworkAccessBehavior.Normal)
            {
                loadComplete = await TryLoadDataFromWebAsync();
            }

            if (!loadComplete)
            {
                loadComplete = await TryFinishLoadFromCacheAsync();
            }

            return loadComplete;
        }
        catch (Exception ex)
        {
            TraceLogger.GetInstance().LogPlatformException(ViewMode.Currency, nameof(TryLoadDataFromWebAsync), ex);
            return false;
        }
        // catch (const exception e)
        // {
        //     TraceLogger.GetInstance().LogStandardException(ViewMode.Currency, __FUNCTIONW__, e);
        //     return false;
        // }
        // catch (...)
        // {
        //     return false;
        // }
    }

    public async Task<bool> TryFinishLoadFromCacheAsync()
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        if (localSettings == null)
        {
            return false;
        }

        if (!localSettings.Values.ContainsKey(CurrencyDataLoaderConstants.CacheLangcodeKey) ||
            !(localSettings.Values[CurrencyDataLoaderConstants.CacheLangcodeKey]).Equals(m_responseLanguage))
        {
            return false;
        }

        StorageFolder localCacheFolder = ApplicationData.Current.LocalCacheFolder;
        if (localCacheFolder == null)
        {
            return false;
        }

        String staticDataResponse =
            await Utilities.ReadFileFromFolder(localCacheFolder, CurrencyDataLoaderConstants.StaticDataFilename);
        String allRatiosResponse =
            await Utilities.ReadFileFromFolder(localCacheFolder, CurrencyDataLoaderConstants.AllRatiosDataFilename);

        List<UCM.CurrencyStaticData> staticData = new();
        CurrencyRatioMap ratioMap = new();

        bool didParse = TryParseWebResponses(staticDataResponse, allRatiosResponse, out staticData, out ratioMap);
        if (!didParse)
        {
            return false;
        }

        m_loadStatus = CurrencyLoadStatus.LoadedFromCache;
        await FinalizeUnits(staticData, ratioMap);

        return true;
    }

    public async Task<bool> TryLoadDataFromWebAsync()
    {
        try
        {
            ResetLoadStatus();

            if (m_networkAccessBehavior == NetworkAccessBehavior.Offline ||
                (m_networkAccessBehavior == NetworkAccessBehavior.OptIn && !m_meteredOverrideSet))
            {
                return false;
            }

            String staticDataResponse = await m_client.GetCurrencyMetadataAsync();
            String allRatiosResponse = await m_client.GetCurrencyRatiosAsync();
            if (staticDataResponse == null || allRatiosResponse == null)
            {
                return false;
            }


            bool didParse = TryParseWebResponses(staticDataResponse, allRatiosResponse,
                out var staticData, out var ratioMap);
            if (!didParse)
            {
                return false;
            }

            // Set the timestamp before saving it below.
            m_cacheTimestamp = Utilities.GetUniversalSystemTime();

            try
            {
                List<(String, String )> cachedFiles =
                    [(CurrencyDataLoaderConstants.StaticDataFilename, staticDataResponse),
                        (CurrencyDataLoaderConstants.AllRatiosDataFilename, allRatiosResponse)];

                StorageFolder localCacheFolder = ApplicationData.Current.LocalCacheFolder;
                foreach (var fileInfo in cachedFiles)
                {
                    await Utilities.WriteFileToFolder(localCacheFolder, fileInfo.Item1, fileInfo.Item2,
                        CreationCollisionOption.ReplaceExisting);
                }

                SaveLangCodeAndTimestamp();
            }
            catch
            {
                // If we fail to save to cache it's okay, we should still continue.
            }

            m_loadStatus = CurrencyLoadStatus.LoadedFromWeb;
            await FinalizeUnits(staticData, ratioMap);

            return true;
        }
        catch (Exception ex)
        {
            TraceLogger.GetInstance().LogPlatformException(ViewMode.Currency, nameof(TryLoadDataFromWebAsync), ex);
            return false;
        }
    }

    public async Task<bool> TryLoadDataFromWebOverrideAsync()
    {
        m_meteredOverrideSet = true;
        bool didLoad = await TryLoadDataFromWebAsync();
        if (!didLoad)
        {
            m_loadStatus = CurrencyLoadStatus.FailedToLoad;
            TraceLogger.GetInstance().LogError(ViewMode.Currency, "TryLoadDataFromWebOverrideAsync",
                "UserRequestedRefreshFailed");
        }

        return didLoad;
    }
// #pragma optimize("", on)

    bool TryParseWebResponses(
        String staticDataJson,
        String allRatiosJson,
        out List<UCM.CurrencyStaticData> staticData,
        out CurrencyRatioMap allRatiosData)
    {
        return TryParseStaticData(staticDataJson, out staticData) & TryParseAllRatiosData(allRatiosJson, out allRatiosData);
    }

// bool TryParseStaticData(   String  rawJson,  ref  List<UCM.CurrencyStaticData> staticData)
// {
//     JsonArray  data = null;
//     if (!JsonArray.TryParse(rawJson, &data))
//     {
//         return false;
//     }
//
//     string countryCode =  "" ;
//     string countryName =  "" ;
//     string currencyCode =  "" ;
//     string currencyName =  "" ;
//     string currencySymbol =  "" ;
//
//     List<string> values = { &countryCode, &countryName, &currencyCode, &currencyName, &currencySymbol };
//
//     assert(valuesCount == STATIC_DATA_PROPERTIESCount);
//     staticData.resize(size_t{ data.Size });
//     for (uint i = 0; i < data.Size; i++)
//     {
//         JsonObject  obj;
//         try
//         {
//             obj = data.GetAt(i).GetObject();
//         }
//         catch (COMException  e)
//         {
//             if (e.HResult == E_ILLEGAL_METHOD_CALL)
//             {
//                 continue;
//             }
//             else
//             {
//                 throw;
//             }
//         }
//
//         for (size_t j = 0; j < valuesCount; j++)
//         {
//             (*values[j]) = obj.GetNamedString(StringReference(STATIC_DATA_PROPERTIES[j].data()));
//         }
//
//         staticData[i] = CurrencyStaticData{ countryCode, countryName, currencyCode, currencyName, currencySymbol };
//     }
//
//     var sortCountryNames = [](UCM.CurrencyStaticData s) { return new String(s.countryName); };
//
//     LocalizationService.GetInstance().Sort<UCM.CurrencyStaticData>(staticData, sortCountryNames);
//
//     return true;
// }


    private bool TryParseStaticData(string rawJson, out List<UCM.CurrencyStaticData> staticData)
    {
        staticData = new List<UCM.CurrencyStaticData>();
        try
        {
            if (JsonArray.TryParse(rawJson, out JsonArray? data) && data != null)
            {
                foreach (var item in data)
                {
                    if (item.ValueType == JsonValueType.Object)
                    {
                        JsonObject obj = item.GetObject();
                        try
                        {
                            // Using KeyValuePair array like C++ version
                            string countryCode = obj.GetNamedString(STATIC_DATA_PROPERTIES[0].Key, "");
                            string countryName = obj.GetNamedString(STATIC_DATA_PROPERTIES[1].Key, "");
                            string currencyCode = obj.GetNamedString(STATIC_DATA_PROPERTIES[2].Key, "");
                            string currencyName = obj.GetNamedString(STATIC_DATA_PROPERTIES[3].Key, "");
                            string currencySymbol = obj.GetNamedString(STATIC_DATA_PROPERTIES[4].Key, "");

                            if (!string.IsNullOrEmpty(currencyCode)) // Basic validation
                            {
                                staticData.Add(new UCM.CurrencyStaticData
                                {
                                    countryCode = countryCode,
                                    countryName = countryName,
                                    currencyCode = currencyCode,
                                    currencyName = currencyName,
                                    currencySymbol = currencySymbol
                                });
                            }
                        }
                        catch (Exception ex) // Catch issues getting specific fields
                        {
                            TraceLogger.GetInstance().LogWarning("TryParseStaticData",
                                $"Skipping item due to parsing error: {ex.Message}");
                            continue; // Skip this item
                        }
                    }
                    // else: item is not an object, skip it. Original C++ catches COMException E_ILLEGAL_METHOD_CALL
                }

                // Sort based on country name using LocalizationService
                // Assuming LocalizationService.Sort takes a List and a Func<T, string> keySelector
                LocalizationService.GetInstance().Sort(staticData, s => s.countryName);

                return true;
            }
        }
        catch (Exception ex) // Catch errors during initial parse or iteration
        {
            TraceLogger.GetInstance().LogPlatformException(ViewMode.Currency, "TryParseStaticData", ex);
        }

        return false;
    }




        private bool TryParseAllRatiosData(string rawJson, out CurrencyRatioMap allRatios)
        {
            allRatios = new CurrencyRatioMap();
            try
            {
                if (JsonArray.TryParse(rawJson, out JsonArray? data) && data != null)
                {
                    string sourceCurrencyCode = LocalizationServiceProperties.DefaultCurrencyCode; // USD

                    foreach (var item in data)
                    {
                         if (item.ValueType == JsonValueType.Object)
                         {
                             JsonObject obj = item.GetObject();
                             try
                             {
                                 // Rt is ratio, An is target currency ISO code.
                                 double relativeRatio = obj.GetNamedNumber(RATIO_KEY, 0.0); // Default to 0 if missing
                                 string targetCurrencyCode = obj.GetNamedString(CURRENCY_CODE_KEY, "");

                                 if (!string.IsNullOrEmpty(targetCurrencyCode) && relativeRatio > 0) // Basic validation
                                 {
                                     allRatios.Add(targetCurrencyCode, new UCM.CurrencyRatio (
                                          relativeRatio,
                                          sourceCurrencyCode,
                                          targetCurrencyCode
                                     ));
                                 }
                             }
                             catch (Exception ex) // Catch issues getting specific fields
                             {
                                 TraceLogger.GetInstance().LogWarning("TryParseAllRatiosData", $"Skipping item due to parsing error: {ex.Message}");
                                 continue; // Skip this item
                             }
                         }
                         // else: item is not an object, skip it.
                    }
                    return true;
                }
            }
            catch (Exception ex) // Catch errors during initial parse or iteration
            {
                TraceLogger.GetInstance().LogPlatformException(ViewMode.Currency, "TryParseAllRatiosData", ex);
            }
            return false;
        }

// FinalizeUnits
//
// There are a few ways we can get the data needed for Currency Converter, including from cache or from web.
// This function accepts the data from any source, and acts as a 'last-steps' for the converter to be ready.
// This includes identifying which units will be selected and building the map of currency ratios.
// #pragma optimize("", off) // Optimization disabled for DevDiv 393321 compatibility
    async Task FinalizeUnits(List<UCM.CurrencyStaticData> staticData, CurrencyRatioMap ratioMap)
    {
        Dictionary<int, (UCM.Unit, double)> idToUnit = new();

        SelectedUnits defaultCurrencies = await GetDefaultFromToCurrency();
        string fromCurrency = defaultCurrencies.first;
        string toCurrency = defaultCurrencies.second;

        lock (m_currencyUnitsMutex)
        {

            int i = 1;
            m_currencyUnits.Clear();
            m_currencyMetadata.Clear();
            bool isConversionSourceSet = false;
            bool isConversionTargetSet = false;
            foreach (UCM.CurrencyStaticData currencyUnit in staticData)
            {
                //var itr = ratioMap.find(currencyUnit.currencyCode);
                if ( ratioMap.TryGetValue(currencyUnit.currencyCode, out var itr) )//itr  != ratioMap.end() && (itr.second).ratio > 0)
                {
                    int id = (int)(UnitConverterUnits.UnitEnd + i);

                    bool isConversionSource = (fromCurrency == currencyUnit.currencyCode);
                    isConversionSourceSet = isConversionSourceSet || isConversionSource;

                    bool isConversionTarget = (toCurrency == currencyUnit.currencyCode);
                    isConversionTargetSet = isConversionTargetSet || isConversionTarget;

                    UCM.Unit unit = new UCM.Unit(
                        id, // id
                        currencyUnit.currencyName, // currencyName
                        currencyUnit.countryName, // countryName
                        currencyUnit.currencyCode, // abbreviation
                        m_isRtlLanguage, // isRtlLanguage
                        isConversionSource, // isConversionSource
                        isConversionTarget // isConversionTarget
                    );

                    m_currencyUnits.Add(unit);
                    m_currencyMetadata.Add(unit, new CurrencyUnitMetadata(currencyUnit.currencySymbol));
                    idToUnit.Add(unit.id, (unit, (itr).ratio));
                    i++;
                }
            }

            if (!isConversionSourceSet || !isConversionTargetSet)
            {
                GuaranteeSelectedUnits();
                defaultCurrencies = (DEFAULT_FROM_CURRENCY, DEFAULT_TO_CURRENCY);
            }

            m_currencyRatioMap.Clear();
            foreach (var unit in m_currencyUnits)
            {
                Dictionary<UCM.Unit, UCM.ConversionData> conversions = new();
                double unitFactor = idToUnit[unit.id].Item2;
                foreach(var itr in idToUnit)
                {

                    UCM.Unit targetUnit = (itr.Value.Item1);
                    double conversionRatio = (itr.Value.Item2);
                    UCM.ConversionData parsedData = new ( 1.0, 0.0, false );
                    Debug.Assert(unitFactor > 0); // divide by zero assert
                    parsedData.ratio = conversionRatio / unitFactor;
                    conversions.Add(targetUnit, parsedData);
                }

                m_currencyRatioMap.Add(unit, conversions);
            }
        } // unlocked m_currencyUnitsMutex

        SaveSelectedUnitsToLocalSettings(defaultCurrencies);
    }
// #pragma optimize("", on)

    void GuaranteeSelectedUnits()
    {
        bool isConversionSourceSet = false;
        bool isConversionTargetSet = false;
        foreach (UCM.Unit unit in m_currencyUnits)
        {
            unit.isConversionSource = false;
            unit.isConversionTarget = false;

            if (!isConversionSourceSet && unit.abbreviation == DEFAULT_FROM_CURRENCY)
            {
                unit.isConversionSource = true;
                isConversionSourceSet = true;
            }

            if (!isConversionTargetSet && unit.abbreviation == DEFAULT_TO_CURRENCY)
            {
                unit.isConversionTarget = true;
                isConversionTargetSet = true;
            }
        }

        // If still not set for either source or target, just select the first currency in the list

        if (m_currencyUnits.Count != 0)
        {
            if (!isConversionSourceSet)
            {
                m_currencyUnits[0].isConversionSource = true;
                isConversionSourceSet = true;
            }

            if (!isConversionTargetSet)
            {
                m_currencyUnits[0].isConversionTarget = true;
                isConversionTargetSet = true;
            }
        }
    }

    void NotifyDataLoadFinished(bool didLoad)
    {
        if (!didLoad)
        {
            m_loadStatus = CurrencyLoadStatus.FailedToLoad;
        }

        if (m_vmCallback != null)
        {
            m_vmCallback.CurrencyDataLoadFinished(didLoad);
        }
    }

    void SaveLangCodeAndTimestamp()
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        if (localSettings == null)
        {
            return;
        }

        localSettings.Values.Add(CurrencyDataLoaderConstants.CacheTimestampKey, m_cacheTimestamp);
        localSettings.Values.Add(CurrencyDataLoaderConstants.CacheLangcodeKey, m_responseLanguage);
    }

    void UpdateDisplayedTimestamp()
    {
        if (m_vmCallback != null)
        {
            string timestamp = GetCurrencyTimestamp();
            bool isWeekOld = Utilities.IsDateTimeOlderThan(m_cacheTimestamp, WEEK_DURATION);

            m_vmCallback.CurrencyTimestampCallback(timestamp, isWeekOld);
        }
    }

    public string GetCurrencyTimestamp()
    {
        DateTime epoch = new DateTime();
        if (m_cacheTimestamp.Ticks != epoch.Ticks)
        {
            DateTimeFormatter dateFormatter = new DateTimeFormatter("shortdate");
            var date = dateFormatter.Format(m_cacheTimestamp);

            DateTimeFormatter timeFormatter = new DateTimeFormatter("shorttime");
            var time = timeFormatter.Format(m_cacheTimestamp);

            return LocalizationStringUtil.GetLocalizedString(m_timestampFormat, date, time);
        }

        return "";
    }

// #pragma optimize("", off) // Optimization disabled for DevDiv 393321 compatibility
    async Task<SelectedUnits> GetDefaultFromToCurrency()
    {
        string fromCurrency = DEFAULT_FROM_CURRENCY;
        string toCurrency = DEFAULT_TO_CURRENCY;

        // First, check if we previously stored the last used currencies.
        bool foundInLocalSettings = TryGetLastUsedCurrenciesFromLocalSettings(out fromCurrency, out toCurrency);
        if (!foundInLocalSettings)
        {
            try
            {
                // Second, see if the current locale has preset defaults in DefaultFromToCurrency.json.
                Uri fileUri = new Uri((DEFAULT_FROM_TO_CURRENCY_FILE_URI));
                StorageFile defaultFromToCurrencyFile = await StorageFile.GetFileFromApplicationUriAsync(fileUri);
                if (defaultFromToCurrencyFile != null)
                {
                    String fileContents = await FileIO.ReadTextAsync(defaultFromToCurrencyFile);
                    JsonObject fromToObject = JsonObject.Parse(fileContents);
                    JsonObject regionalDefaults = fromToObject.GetNamedObject(m_responseLanguage);

                    // Get both values before assignment in-case either fails.
                    String selectedFrom = regionalDefaults.GetNamedString((FROM_KEY));
                    String selectedTo = regionalDefaults.GetNamedString((TO_KEY));

                    fromCurrency = selectedFrom;
                    toCurrency = selectedTo;
                }
            }
            catch
            {
            }
        }

        return (fromCurrency, toCurrency);
    }
// #pragma optimize("", on)

    bool TryGetLastUsedCurrenciesFromLocalSettings(out string fromCurrency, out string toCurrency)
    {

        String fromKey = UnitConverterResourceKeys.CurrencyUnitFromKey;
        String toKey = UnitConverterResourceKeys.CurrencyUnitToKey;
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        if (localSettings != null && localSettings.Values != null)
        {
            IPropertySet values = localSettings.Values;
            if (values.ContainsKey(fromKey) && values.ContainsKey(toKey))
            {
                fromCurrency = (String)(values[fromKey]);
                toCurrency = (String)(values[toKey]);

                return true;
            }
        }

        fromCurrency = string.Empty;
        toCurrency = string.Empty;

        return false;
    }

    void SaveSelectedUnitsToLocalSettings(SelectedUnits selectedUnits)
    {
        String fromKey = UnitConverterResourceKeys.CurrencyUnitFromKey;
        String toKey = UnitConverterResourceKeys.CurrencyUnitToKey;
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        if (localSettings != null && localSettings.Values != null)
        {
            IPropertySet values = localSettings.Values;
            values.TryAdd(fromKey, (selectedUnits.first));
            values.TryAdd(toKey, (selectedUnits.second));
        }
    }
}
