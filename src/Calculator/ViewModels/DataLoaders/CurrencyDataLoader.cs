// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Globalization;
using CalcEngine;
using CalculatorApp.Services.Settings;
using CalculatorApp.ViewModel.Common;
using UCM = UnitConversionManager;

namespace CalculatorApp.ViewModel.DataLoaders;

public sealed partial class CurrencyDataLoader : UCM.IConverterDataLoader, UCM.ICurrencyConverterDataLoader
{
    internal const string CacheTimestampKey = "CURRENCY_CONVERTER_TIMESTAMP";
    internal const string CacheFilename = "currency-cache-v1.json";
    internal static readonly TimeSpan CacheRefreshAge = TimeSpan.FromDays(1);
    internal static readonly TimeSpan CacheWarningAge = TimeSpan.FromDays(7);
    private readonly ICurrencyRateProvider _rateProvider;
    private readonly ICurrencyNameProvider _nameProvider;
    private readonly ISettingsStore _settingsStore;
    private readonly NetworkManager _networkManager = new();
    // Currency ratios use rational arithmetic only. The default constructor
    // loads RatPak's precomputed constants instead of regenerating unused
    // transcendental constants at RatPakDecimal's arithmetic precision.
    private readonly RatPak _ratPak = new();
    private readonly int _ownerThreadId;
    private readonly NumberFormatInfo _numberFormat;
    private readonly string _cachePath;
    private readonly string _ratioFormat;
    private readonly string _timestampFormat;
    private readonly bool _isRtlLanguage;
    private CurrencyDataLoaderCurrencyDataSnapshot _currencyData = CurrencyDataLoaderCurrencyDataSnapshot.Empty;
    private UCM.IViewModelCurrencyCallback? _viewModelCallback;
    private NetworkAccessBehavior _networkAccessBehavior = NetworkAccessBehavior.Normal;
    private CurrencyLoadStatus _loadStatus;
    private DateTimeOffset _cacheTimestamp;
    private Task<bool>? _initialLoadTask;
    private Task? _automaticRefreshTask;
    public CurrencyDataLoader(ICurrencyRateProvider? rateProvider = null, ICurrencyNameProvider? nameProvider = null, string? cachePath = null, ISettingsStore? settingsStore = null, int? ownerThreadId = null)
        : this(
            rateProvider,
            nameProvider,
            cachePath,
            settingsStore,
            ownerThreadId,
            CaptureLocalizedResources())
    {
    }

    internal CurrencyDataLoader(
        ICurrencyRateProvider? rateProvider,
        ICurrencyNameProvider? nameProvider,
        string? cachePath,
        ISettingsStore? settingsStore,
        int? ownerThreadId,
        CurrencyDataLoaderLocalizedResources localizedResources)
    {
        _rateProvider = rateProvider ?? new CurrencyHttpClient();
        _nameProvider = nameProvider ?? new CldrCurrencyNameProvider();
        _settingsStore = settingsStore ?? new InMemorySettingsStore();
        _ownerThreadId = ownerThreadId ?? Environment.CurrentManagedThreadId;
        _cachePath = cachePath ?? GetDefaultCachePath();
        _isRtlLanguage = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
        _numberFormat = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        _ratioFormat = localizedResources.RatioFormat;
        _timestampFormat = localizedResources.TimestampFormat;
        if (_rateProvider is CurrencyHttpClient client)
        {
            client.Initialize("USD", CultureInfo.CurrentUICulture.Name);
        }
    }

    internal static CurrencyDataLoaderLocalizedResources CaptureLocalizedResources()
    {
        AppResourceProvider resources = AppResourceProvider.Instance;
        return new CurrencyDataLoaderLocalizedResources(
            resources.GetResourceString("CurrencyFromToRatioFormat"),
            resources.GetResourceString("CurrencyTimestampFormat"));
    }

    public CurrencyLoadStatus LoadStatus => _loadStatus;
    public bool LoadFinished => _loadStatus != CurrencyLoadStatus.NotLoaded;
    public bool LoadedFromCache => _loadStatus == CurrencyLoadStatus.LoadedFromCache;
    public bool LoadedFromWeb => _loadStatus == CurrencyLoadStatus.LoadedFromWeb;

    private static string GetDefaultCachePath()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = AppContext.BaseDirectory;
        }

        return Path.Combine(root, "io.github.jmacato.calculator", CacheFilename);
    }
}
