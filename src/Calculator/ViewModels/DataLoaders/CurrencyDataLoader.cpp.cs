// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using CalculatorApp.ViewModel.Common;
using UCM = UnitConversionManager;

namespace CalculatorApp.ViewModel.DataLoaders;

public partial class CurrencyDataLoader
{
    public async void LoadData()
    {
        if (_initialLoadTask is { IsCompleted: false })
        {
            return;
        }

        if (LoadFinished)
        {
            return;
        }

        _initialLoadTask = LoadInitialDataAsync();
        bool didLoad = await _initialLoadTask;
        NotifyDataLoadFinished(didLoad);
    }

    private async Task<bool> LoadInitialDataAsync()
    {
        CurrencyRateSnapshot? cached = await ReadCacheAsync();
        if (cached is not null &&
            DateTimeOffset.UtcNow - cached.FetchedAtUtc <= CacheRefreshAge)
        {
            FinalizeUnits(cached);
            _loadStatus = CurrencyLoadStatus.LoadedFromCache;
            return true;
        }

        if (await TryLoadDataFromWebAsync())
        {
            return true;
        }

        if (cached is not null)
        {
            FinalizeUnits(cached);
            _loadStatus = CurrencyLoadStatus.LoadedFromCache;
            return true;
        }

        _loadStatus = CurrencyLoadStatus.FailedToLoad;
        return false;
    }

    public IList<UCM.Category> GetOrderedCategories() => [];

    public IList<UCM.Unit> GetOrderedUnits(UCM.Category category)
    {
        _ = category;
        lock (_currencyUnitsMutex)
        {
            return _currencyUnits;
        }
    }

    public Dictionary<UCM.Unit, UCM.ConversionData> LoadOrderedRatios(UCM.Unit unit)
    {
        lock (_currencyUnitsMutex)
        {
            return _currencyRatioMap.TryGetValue(unit, out var ratios)
                ? ratios
                : [];
        }
    }

    public bool SupportsCategory(UCM.Category target) =>
        target.Id == NavCategoryStates.Serialize(ViewMode.Currency);

    public void SetViewModelCallback(UCM.IViewModelCurrencyCallback callback)
    {
        _viewModelCallback = callback;
        callback.NetworkBehaviorChanged((int)NetworkAccessBehavior.Normal);
    }

    public (string, string) GetCurrencySymbols(UCM.Unit unit1, UCM.Unit unit2)
    {
        lock (_currencyUnitsMutex)
        {
            string symbol1 = _currencyMetadata.TryGetValue(unit1, out var first)
                ? first.Symbol
                : unit1.Abbreviation;
            string symbol2 = _currencyMetadata.TryGetValue(unit2, out var second)
                ? second.Symbol
                : unit2.Abbreviation;
            return (symbol1, symbol2);
        }
    }

    public (string, string) GetCurrencyRatioEquality(UCM.Unit unit1, UCM.Unit unit2)
    {
        lock (_currencyUnitsMutex)
        {
            if (!_currencyRatioMap.TryGetValue(unit1, out var ratios) ||
                !ratios.TryGetValue(unit2, out UCM.ConversionData? conversion))
            {
                return (string.Empty, string.Empty);
            }

            double rounded = RoundCurrencyRatio(conversion.Ratio);
            string formatted = rounded.ToString("G15", CultureInfo.CurrentCulture);
            string visible = LocalizationStringUtil.GetLocalizedString(
                _ratioFormat,
                LocalizationSettings.GetInstance().GetDigitSymbolFromEnUsDigit('1').ToString(),
                unit1.Abbreviation,
                formatted,
                unit2.Abbreviation);
            string accessible = LocalizationStringUtil.GetLocalizedString(
                _ratioFormat,
                "1",
                unit1.AccessibleName,
                formatted,
                unit2.AccessibleName);
            return (visible, accessible);
        }
    }

    public static double RoundCurrencyRatio(double ratio)
    {
        if (ratio <= 0 || double.IsNaN(ratio) || double.IsInfinity(ratio))
        {
            return ratio;
        }

        int decimals = 4;
        if (ratio < 1)
        {
            decimals = Math.Max(decimals, (int)-Math.Floor(Math.Log10(ratio)) + 3);
        }

        return Math.Round(ratio, Math.Min(decimals, 15), MidpointRounding.AwayFromZero);
    }

    public string GetCurrencyTimestamp()
    {
        if (_cacheTimestamp == default)
        {
            return string.Empty;
        }

        string date = _cacheTimestamp.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);
        string time = _cacheTimestamp.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
        return LocalizationStringUtil.GetLocalizedString(_timestampFormat, date, time);
    }

    public async Task<bool> TryLoadDataFromCacheAsync()
    {
        CurrencyRateSnapshot? snapshot = await ReadCacheAsync();
        if (snapshot is null)
        {
            return false;
        }

        FinalizeUnits(snapshot);
        _loadStatus = CurrencyLoadStatus.LoadedFromCache;
        UpdateDisplayedTimestamp();
        return true;
    }

    public async Task<bool> TryLoadDataFromWebAsync()
    {
        try
        {
            CurrencyRateSnapshot snapshot = await _rateProvider.GetLatestRatesAsync();
            ValidateSnapshot(snapshot);
            FinalizeUnits(snapshot);
            _loadStatus = CurrencyLoadStatus.LoadedFromWeb;
            await WriteCacheAtomicallyAsync(snapshot);
            UpdateDisplayedTimestamp();
            return true;
        }
        catch (Exception exception)
        {
            _loadStatus = CurrencyLoadStatus.FailedToLoad;
            TraceLogger.GetInstance().LogPlatformException(
                ViewMode.Currency,
                nameof(TryLoadDataFromWebAsync),
                exception);
            return false;
        }
    }

    public async Task<bool> TryLoadDataFromWebOverrideAsync()
    {
        bool didLoad = await TryLoadDataFromWebAsync();
        if (!didLoad)
        {
            TraceLogger.GetInstance().LogError(
                ViewMode.Currency,
                nameof(TryLoadDataFromWebOverrideAsync),
                "UserRequestedRefreshFailed");
        }

        return didLoad;
    }

    private void FinalizeUnits(CurrencyRateSnapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        Dictionary<string, double> rates = snapshot.Rates
            .Where(rate => rate.Rate > 0 && !string.IsNullOrWhiteSpace(rate.QuoteCurrency))
            .GroupBy(rate => rate.QuoteCurrency, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Rate,
                StringComparer.OrdinalIgnoreCase);

        Dictionary<string, CurrencyMetadataRecord> metadata = snapshot.Currencies
            .Where(item => !string.IsNullOrWhiteSpace(item.IsoCode))
            .GroupBy(item => item.IsoCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        string preferredFrom = GetRegionalCurrencyCode();
        if (!rates.ContainsKey(preferredFrom))
        {
            preferredFrom = rates.ContainsKey("USD") ? "USD" : rates.Keys.First();
        }

        string preferredTo = preferredFrom.Equals("EUR", StringComparison.OrdinalIgnoreCase)
            ? "USD"
            : rates.ContainsKey("EUR") ? "EUR" : rates.Keys.First();

        lock (_currencyUnitsMutex)
        {
            _currencyUnits = [];
            _currencyRatioMap = [];
            _currencyMetadata = [];

            int id = (int)UnitConverterUnits.UnitEnd + 1;
            foreach ((string isoCode, double _) in rates.OrderBy(pair => pair.Key,
                         StringComparer.OrdinalIgnoreCase))
            {
                metadata.TryGetValue(isoCode, out CurrencyMetadataRecord? fallback);
                CurrencyDisplayMetadata display = _nameProvider.GetCurrency(
                    isoCode,
                    fallback?.Name ?? isoCode,
                    fallback?.Symbol ?? isoCode);
                UCM.Unit unit = new(
                    id++,
                    $"{isoCode} — {display.Name}",
                    isoCode,
                    isoCode.Equals(preferredFrom, StringComparison.OrdinalIgnoreCase),
                    isoCode.Equals(preferredTo, StringComparison.OrdinalIgnoreCase),
                    false)
                {
                    AccessibleName = $"{isoCode} {display.Name}"
                };
                _currencyUnits.Add(unit);
                _currencyMetadata[unit] = new CurrencyUnitMetadata(display.Symbol);
            }

            foreach (UCM.Unit source in _currencyUnits)
            {
                double sourceRate = rates[source.Abbreviation];
                Dictionary<UCM.Unit, UCM.ConversionData> conversions = [];
                foreach (UCM.Unit target in _currencyUnits)
                {
                    conversions[target] = new UCM.ConversionData(
                        rates[target.Abbreviation] / sourceRate,
                        0,
                        false);
                }
                _currencyRatioMap[source] = conversions;
            }
        }

        _cacheTimestamp = snapshot.FetchedAtUtc;
    }

    private static string GetRegionalCurrencyCode()
    {
        try
        {
            return RegionInfo.CurrentRegion.ISOCurrencySymbol;
        }
        catch (ArgumentException)
        {
            return "USD";
        }
    }

    private static void ValidateSnapshot(CurrencyRateSnapshot snapshot)
    {
        if (snapshot.Currencies.Count == 0 || snapshot.Rates.Count == 0 ||
            snapshot.Rates.Any(rate => rate.Rate <= 0 || !double.IsFinite(rate.Rate)))
        {
            throw new InvalidDataException("Currency data is empty or malformed.");
        }
    }

    private async Task<CurrencyRateSnapshot?> ReadCacheAsync()
    {
        try
        {
            if (!File.Exists(_cachePath))
            {
                return null;
            }

            await using FileStream stream = File.OpenRead(_cachePath);
            CurrencyRateSnapshot? snapshot = await JsonSerializer.DeserializeAsync(
                stream,
                CurrencyJsonContext.Default.CurrencyRateSnapshot);
            if (snapshot is not null)
            {
                ValidateSnapshot(snapshot);
            }
            return snapshot;
        }
        catch (Exception exception)
        {
            TryBackupCorruptCache();
            TraceLogger.GetInstance().LogPlatformException(
                ViewMode.Currency,
                nameof(ReadCacheAsync),
                exception);
            return null;
        }
    }

    private async Task WriteCacheAtomicallyAsync(CurrencyRateSnapshot snapshot)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = _cachePath + ".tmp";
            await using (FileStream stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    snapshot,
                    CurrencyJsonContext.Default.CurrencyRateSnapshot);
                await stream.FlushAsync();
            }

            File.Move(temporaryPath, _cachePath, true);
        }
        catch (Exception exception)
        {
            TraceLogger.GetInstance().LogPlatformException(
                ViewMode.Currency,
                nameof(WriteCacheAtomicallyAsync),
                exception);
        }
    }

    private void TryBackupCorruptCache()
    {
        try
        {
            if (File.Exists(_cachePath))
            {
                File.Move(
                    _cachePath,
                    _cachePath + $".corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
                    true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void NotifyDataLoadFinished(bool didLoad)
    {
        _viewModelCallback?.NetworkBehaviorChanged(
            (int)(didLoad ? NetworkAccessBehavior.Normal : NetworkAccessBehavior.Offline));
        UpdateDisplayedTimestamp();
        _viewModelCallback?.CurrencyDataLoadFinished(didLoad);
    }

    private void UpdateDisplayedTimestamp()
    {
        _viewModelCallback?.CurrencyTimestampCallback(
            GetCurrencyTimestamp(),
            _cacheTimestamp != default &&
            DateTimeOffset.UtcNow - _cacheTimestamp > CacheWarningAge);
    }
}
