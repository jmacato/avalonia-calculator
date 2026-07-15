// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using CalcEngine;
using CalculatorApp.Services.Settings;
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

        RegisterForNetworkBehaviorChanges();
        _initialLoadTask = LoadInitialDataAndNotifyAsync();
        await _initialLoadTask;
    }

    private async Task<bool> LoadInitialDataAndNotifyAsync()
    {
        bool didLoad = await LoadInitialDataAsync();
        NotifyDataLoadFinished(didLoad);
        return didLoad;
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
        OnNetworkBehaviorChanged(_networkAccessBehavior);
    }

    private void RegisterForNetworkBehaviorChanges()
    {
        _networkManager.NetworkBehaviorChanged -= OnNetworkBehaviorChanged;
        _networkManager.NetworkBehaviorChanged += OnNetworkBehaviorChanged;
        OnNetworkBehaviorChanged(NetworkManager.GetNetworkAccessBehavior());
    }

    private void OnNetworkBehaviorChanged(NetworkAccessBehavior newBehavior)
    {
        NetworkAccessBehavior previousBehavior = _networkAccessBehavior;
        _networkAccessBehavior = newBehavior;
        _viewModelCallback?.NetworkBehaviorChanged((int)newBehavior);

        if (previousBehavior == NetworkAccessBehavior.Offline &&
            newBehavior == NetworkAccessBehavior.Normal &&
            _settingsStore.Current.AutomaticCurrencyRefresh &&
            (_automaticRefreshTask is null || _automaticRefreshTask.IsCompleted))
        {
            _automaticRefreshTask = RefreshAfterNetworkAvailableAsync();
        }
    }

    private async Task RefreshAfterNetworkAvailableAsync()
    {
        // The network event can arrive immediately after registration, before
        // LoadData has stored the task. Yield once so the initial load remains
        // the only operation that initializes the converter data.
        await Task.Yield();
        if (_initialLoadTask is not null)
        {
            await _initialLoadTask;
        }

        if (!_settingsStore.Current.AutomaticCurrencyRefresh ||
            _networkAccessBehavior != NetworkAccessBehavior.Normal ||
            !CurrencyDataNeedsRefresh())
        {
            return;
        }

        bool didLoad = await TryLoadDataFromWebAsync();
        NotifyDataLoadFinished(didLoad);
    }

    private bool CurrencyDataNeedsRefresh() =>
        _loadStatus == CurrencyLoadStatus.FailedToLoad ||
        _cacheTimestamp == default ||
        DateTimeOffset.UtcNow - _cacheTimestamp > CacheRefreshAge;

    public (string, string) GetCurrencySymbols(UCM.Unit unit1, UCM.Unit unit2)
    {
        lock (_currencyUnitsMutex)
        {
            // Preserve the WinUI loader contract: these are currency symbols,
            // not generic unit abbreviations. Both values must be currencies.
            return _currencyMetadata.TryGetValue(unit1, out var first) &&
                   _currencyMetadata.TryGetValue(unit2, out var second)
                ? (first.Symbol, second.Symbol)
                : (string.Empty, string.Empty);
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

            Rational ratio = RatPakDecimal.Parse(_ratPak, conversion.RatioNumerator) /
                             RatPakDecimal.Parse(_ratPak, conversion.RatioDenominator);
            int decimals = 4;
            int exponent = RatPakDecimal.GetDecimalExponent(_ratPak, ratio);
            if (exponent < 0)
            {
                decimals = Math.Max(decimals, -exponent + 3);
            }

            string formatted = RatPakDecimal.FormatFixed(
                _ratPak,
                ratio,
                Math.Min(decimals, 15),
                RatPakRoundingMode.AwayFromZero);
            formatted = FormatCurrencyRatio(formatted, _numberFormat);
            LocalizationSettings localization = LocalizationSettings.GetInstance();
            string one = localization.GetDigitSymbolFromEnUsDigit('1').ToString();
            string visible = LocalizationStringUtil.GetLocalizedString(
                _ratioFormat,
                one,
                unit1.Abbreviation,
                formatted,
                unit2.Abbreviation);
            string accessible = LocalizationStringUtil.GetLocalizedString(
                _ratioFormat,
                one,
                unit1.AccessibleName,
                formatted,
                unit2.AccessibleName);
            return (visible, accessible);
        }
    }

    private static string FormatCurrencyRatio(
        string invariantValue,
        NumberFormatInfo numberFormat)
    {
        const int minimumFractionDigits = 2;

        bool isNegative = invariantValue.StartsWith("-", StringComparison.Ordinal);
        string unsignedValue = isNegative ? invariantValue[1..] : invariantValue;
        int decimalPosition = unsignedValue.IndexOf('.');
        string whole = decimalPosition < 0
            ? unsignedValue
            : unsignedValue[..decimalPosition];
        string fraction = decimalPosition < 0
            ? string.Empty
            : unsignedValue[(decimalPosition + 1)..];

        while (fraction.Length > minimumFractionDigits && fraction.EndsWith('0'))
        {
            fraction = fraction[..^1];
        }

        fraction = fraction.PadRight(minimumFractionDigits, '0');
        string grouped = CurrencyDisplayFormatter.ApplyGrouping(
            whole,
            numberFormat.NumberGroupSeparator,
            numberFormat.NumberGroupSizes);
        string localized = grouped + numberFormat.NumberDecimalSeparator + fraction;
        localized = CurrencyDisplayFormatter.LocalizeDigits(localized, numberFormat);
        return isNegative ? numberFormat.NegativeSign + localized : localized;
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
        if (_networkAccessBehavior == NetworkAccessBehavior.Offline)
        {
            return false;
        }

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
        Dictionary<string, decimal> rates = snapshot.Rates
            .Where(rate => rate.Rate > 0 && !string.IsNullOrWhiteSpace(rate.QuoteCurrency))
            .GroupBy(rate => rate.QuoteCurrency, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Rate,
                StringComparer.OrdinalIgnoreCase);

        Dictionary<string, CurrencyMetadataRecord> metadata = snapshot.Currencies
            .Where(item => !string.IsNullOrWhiteSpace(item.IsoCode))
            .GroupBy(item => item.IsoCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(),
                StringComparer.OrdinalIgnoreCase);

        AppSettings settings = _settingsStore.Current;
        string preferredFrom = settings.CurrencyUnitFrom;
        if (!rates.ContainsKey(preferredFrom))
        {
            preferredFrom = GetRegionalCurrencyCode();
        }

        if (!rates.ContainsKey(preferredFrom))
        {
            preferredFrom = rates.ContainsKey("USD") ? "USD" : rates.Keys.First();
        }

        string preferredTo = settings.CurrencyUnitTo;
        if (!rates.ContainsKey(preferredTo))
        {
            preferredTo = preferredFrom.Equals("EUR", StringComparison.OrdinalIgnoreCase)
                ? "USD"
                : rates.ContainsKey("EUR") ? "EUR" : rates.Keys.First();
        }

        lock (_currencyUnitsMutex)
        {
            _currencyUnits = [];
            _currencyRatioMap = [];
            _currencyMetadata = [];

            int id = (int)UnitConverterUnits.UnitEnd + 1;
            foreach ((string isoCode, decimal _) in rates.OrderBy(pair => pair.Key,
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
                _currencyMetadata[unit] = new CurrencyUnitMetadata(
                    display.Symbol,
                    display.FractionDigits);
            }

            foreach (UCM.Unit source in _currencyUnits)
            {
                decimal sourceRate = rates[source.Abbreviation];
                Dictionary<UCM.Unit, UCM.ConversionData> conversions = [];
                foreach (UCM.Unit target in _currencyUnits)
                {
                    conversions[target] = new UCM.ConversionData(
                        rates[target.Abbreviation].ToString(CultureInfo.InvariantCulture),
                        sourceRate.ToString(CultureInfo.InvariantCulture),
                        "0",
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
        if (snapshot.FetchedAtUtc == default ||
            string.IsNullOrWhiteSpace(snapshot.BaseCurrency) ||
            snapshot.Currencies is not { Count: > 0 } ||
            snapshot.Currencies.Any(currency =>
                currency is null || string.IsNullOrWhiteSpace(currency.IsoCode)) ||
            snapshot.Rates is not { Count: > 0 } ||
            snapshot.Rates.Any(rate =>
                rate is null ||
                rate.Rate <= 0 ||
                string.IsNullOrWhiteSpace(rate.BaseCurrency) ||
                !rate.BaseCurrency.Equals(
                    snapshot.BaseCurrency,
                    StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(rate.QuoteCurrency)))
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
        if (!didLoad)
        {
            _loadStatus = CurrencyLoadStatus.FailedToLoad;
        }

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
