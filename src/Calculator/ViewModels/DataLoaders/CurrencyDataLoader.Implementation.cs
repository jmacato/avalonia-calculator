// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using CalcEngine;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using UCM = UnitConversionManager;

namespace CalculatorApp.ViewModel.DataLoaders;

public sealed partial class CurrencyDataLoader
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
        await _initialLoadTask.ConfigureAwait(true);
    }

    private async Task<bool> LoadInitialDataAndNotifyAsync()
    {
        bool didLoad = await LoadInitialDataAsync().ConfigureAwait(true);
        NotifyDataLoadFinished(didLoad);
        return didLoad;
    }

    private async Task<bool> LoadInitialDataAsync()
    {
        CurrencyRateSnapshot? cached = await ReadCacheAsync().ConfigureAwait(true);
        if (cached is not null &&
            DateTimeOffset.UtcNow - cached.FetchedAtUtc <= CacheRefreshAge)
        {
            FinalizeUnits(cached);
            _loadStatus = CurrencyLoadStatus.LoadedFromCache;
            return true;
        }

        if (await TryLoadDataFromWebAsync().ConfigureAwait(true))
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

    public IList<UCM.Category> GetOrderedCategories()
    {
        return [];
    }

    public IList<UCM.Unit> GetOrderedUnits(UCM.Category category)
    {
        _ = category;
        return Volatile.Read(ref _currencyData).Units;
    }

    public Dictionary<UCM.Unit, UCM.ConversionData> LoadOrderedRatios(UCM.Unit unit)
    {
        CurrencyDataLoaderCurrencyDataSnapshot data = Volatile.Read(ref _currencyData);
        return data.Ratios.TryGetValue(unit, out var ratios) ? ratios : [];
    }

    public bool SupportsCategory(UCM.Category target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.Id == NavCategoryStates.Serialize(ViewMode.Currency);
    }

    public void SetViewModelCallback(UCM.IViewModelCurrencyCallback callback)
    {
        _viewModelCallback = callback;
        OnNetworkBehaviorChanged(this, new NetworkBehaviorChangedEventArgs(_networkAccessBehavior));
    }

    private void RegisterForNetworkBehaviorChanges()
    {
        _networkManager.NetworkBehaviorChanged -= OnNetworkBehaviorChanged;
        _networkManager.NetworkBehaviorChanged += OnNetworkBehaviorChanged;
        OnNetworkBehaviorChanged(this, new NetworkBehaviorChangedEventArgs(NetworkManager.GetNetworkAccessBehavior()));
    }

    private void OnNetworkBehaviorChanged(object? sender, NetworkBehaviorChangedEventArgs e)
    {
        _ = sender;
        NetworkAccessBehavior newBehavior = e.Behavior;
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
            await _initialLoadTask.ConfigureAwait(true);
        }

        if (!_settingsStore.Current.AutomaticCurrencyRefresh ||
            _networkAccessBehavior != NetworkAccessBehavior.Normal ||
            !CurrencyDataNeedsRefresh())
        {
            return;
        }

        bool didLoad = await TryLoadDataFromWebAsync().ConfigureAwait(true);
        NotifyDataLoadFinished(didLoad);
    }

    private bool CurrencyDataNeedsRefresh()
    {
        return _loadStatus == CurrencyLoadStatus.FailedToLoad ||
               _cacheTimestamp == default ||
               DateTimeOffset.UtcNow - _cacheTimestamp > CacheRefreshAge;
    }

    public (string, string) GetCurrencySymbols(UCM.Unit unit1, UCM.Unit unit2)
    {
        CurrencyDataLoaderCurrencyDataSnapshot data = Volatile.Read(ref _currencyData);
        // Preserve the WinUI loader contract: these are currency symbols,
        // not generic unit abbreviations. Both values must be currencies.
        return data.Metadata.TryGetValue(unit1, out var first) &&
               data.Metadata.TryGetValue(unit2, out var second)
            ? (first.Symbol, second.Symbol)
            : (string.Empty, string.Empty);
    }

    public (string, string) GetCurrencyRatioEquality(UCM.Unit unit1, UCM.Unit unit2)
    {
        ArgumentNullException.ThrowIfNull(unit1);
        ArgumentNullException.ThrowIfNull(unit2);
        if (Environment.CurrentManagedThreadId != _ownerThreadId)
        {
            throw new InvalidOperationException("Currency formatting must remain on its owning UI thread.");
        }

        CurrencyDataLoaderCurrencyDataSnapshot data = Volatile.Read(ref _currencyData);
        if (!data.Ratios.TryGetValue(unit1, out var ratios) ||
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
        LocalizationSettings localization = LocalizationSettings.Instance;
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

    private static string FormatCurrencyRatio(
        string invariantValue,
        NumberFormatInfo numberFormat)
    {
        const int minimumFractionDigits = 2;

        bool isNegative = invariantValue.StartsWith('-');
        string unsignedValue = isNegative ? invariantValue[1..] : invariantValue;
        int decimalPosition = unsignedValue.IndexOf('.', StringComparison.Ordinal);
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
        CurrencyRateSnapshot? snapshot = await ReadCacheAsync().ConfigureAwait(true);
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
            CurrencyRateSnapshot snapshot = await _rateProvider.GetLatestRatesAsync().ConfigureAwait(true);
            ValidateSnapshot(snapshot);
            FinalizeUnits(snapshot);
            _loadStatus = CurrencyLoadStatus.LoadedFromWeb;
            await WriteCacheAtomicallyAsync(snapshot).ConfigureAwait(true);
            UpdateDisplayedTimestamp();
            return true;
        }
        catch (HttpRequestException exception)
        {
            return HandleWebLoadFailure(exception);
        }
        catch (OperationCanceledException exception)
        {
            return HandleWebLoadFailure(exception);
        }
        catch (InvalidDataException exception)
        {
            return HandleWebLoadFailure(exception);
        }
        catch (JsonException exception)
        {
            return HandleWebLoadFailure(exception);
        }
        catch (IOException exception)
        {
            return HandleWebLoadFailure(exception);
        }
        catch (NotSupportedException exception)
        {
            return HandleWebLoadFailure(exception);
        }
    }

    private bool HandleWebLoadFailure(Exception exception)
    {
        _loadStatus = CurrencyLoadStatus.FailedToLoad;
        TraceLogger.LogPlatformException(
            ViewMode.Currency,
            nameof(TryLoadDataFromWebAsync),
            exception);
        return false;
    }

    public async Task<bool> TryLoadDataFromWebOverrideAsync()
    {
        bool didLoad = await TryLoadDataFromWebAsync().ConfigureAwait(true);
        if (!didLoad)
        {
            TraceLogger.LogError(
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

        List<UCM.Unit> currencyUnits = [];
        Dictionary<UCM.Unit, Dictionary<UCM.Unit, UCM.ConversionData>> currencyRatioMap = [];
        Dictionary<UCM.Unit, CurrencyUnitMetadata> currencyMetadata = [];

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
            currencyUnits.Add(unit);
            currencyMetadata[unit] = new CurrencyUnitMetadata(
                display.Symbol,
                display.FractionDigits);
        }

        foreach (UCM.Unit source in currencyUnits)
        {
            decimal sourceRate = rates[source.Abbreviation];
            Dictionary<UCM.Unit, UCM.ConversionData> conversions = [];
            foreach (UCM.Unit target in currencyUnits)
            {
                conversions[target] = new UCM.ConversionData(
                    rates[target.Abbreviation].ToString(CultureInfo.InvariantCulture),
                    sourceRate.ToString(CultureInfo.InvariantCulture),
                    "0",
                    false);
            }
            currencyRatioMap[source] = conversions;
        }

        Volatile.Write(
            ref _currencyData,
            new CurrencyDataLoaderCurrencyDataSnapshot(currencyUnits, currencyRatioMap, currencyMetadata));
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
            snapshot.Currencies.IsDefaultOrEmpty ||
            snapshot.Currencies.Any(currency =>
                currency is null || string.IsNullOrWhiteSpace(currency.IsoCode)) ||
            snapshot.Rates.IsDefaultOrEmpty ||
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

            FileStream stream = File.OpenRead(_cachePath);
            CurrencyRateSnapshot? snapshot;
            await using (stream.ConfigureAwait(false))
            {
                snapshot = await JsonSerializer.DeserializeAsync(
                    stream,
                    CurrencyJsonContext.Default.CurrencyRateSnapshot).ConfigureAwait(false);
            }
            if (snapshot is not null)
            {
                ValidateSnapshot(snapshot);
            }
            return snapshot;
        }
        catch (JsonException exception)
        {
            return HandleCacheReadFailure(exception);
        }
        catch (InvalidDataException exception)
        {
            return HandleCacheReadFailure(exception);
        }
        catch (IOException exception)
        {
            return HandleCacheReadFailure(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return HandleCacheReadFailure(exception);
        }
        catch (NotSupportedException exception)
        {
            return HandleCacheReadFailure(exception);
        }
    }

    private CurrencyRateSnapshot? HandleCacheReadFailure(Exception exception)
    {
        TryBackupCorruptCache();
        TraceLogger.LogPlatformException(
            ViewMode.Currency,
            nameof(ReadCacheAsync),
            exception);
        return null;
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
            FileStream stream = File.Create(temporaryPath);
            await using (stream.ConfigureAwait(false))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    snapshot,
                    CurrencyJsonContext.Default.CurrencyRateSnapshot).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }

            File.Move(temporaryPath, _cachePath, true);
        }
        catch (JsonException exception)
        {
            LogCacheWriteFailure(exception);
        }
        catch (IOException exception)
        {
            LogCacheWriteFailure(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            LogCacheWriteFailure(exception);
        }
        catch (NotSupportedException exception)
        {
            LogCacheWriteFailure(exception);
        }
    }

    private static void LogCacheWriteFailure(Exception exception)
    {
        TraceLogger.LogPlatformException(
            ViewMode.Currency,
            nameof(WriteCacheAtomicallyAsync),
            exception);
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
