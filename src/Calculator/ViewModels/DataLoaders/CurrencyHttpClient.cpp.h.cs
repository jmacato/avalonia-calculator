// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json.Serialization;

namespace CalculatorApp.ViewModel.DataLoaders;

public interface ICurrencyRateProvider
{
    Task<CurrencyRateSnapshot> GetLatestRatesAsync(CancellationToken cancellationToken = default);
}

public interface ICurrencyNameProvider
{
    CurrencyDisplayMetadata GetCurrency(string isoCode, string fallbackName, string fallbackSymbol);
}

public sealed record CurrencyDisplayMetadata(string Name, string Symbol, int FractionDigits);

public sealed class CurrencyRateSnapshot
{
    public DateTimeOffset FetchedAtUtc { get; set; }
    public string BaseCurrency { get; set; } = "USD";
    public List<CurrencyMetadataRecord> Currencies { get; set; } = [];
    public List<CurrencyRateRecord> Rates { get; set; } = [];
}

public sealed class CurrencyMetadataRecord
{
    [JsonPropertyName("iso_code")]
    public string IsoCode { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;
}

public sealed class CurrencyRateRecord
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("base")]
    public string BaseCurrency { get; set; } = string.Empty;

    [JsonPropertyName("quote")]
    public string QuoteCurrency { get; set; } = string.Empty;

    [JsonPropertyName("rate")]
    public decimal Rate { get; set; }
}

[JsonSerializable(typeof(List<CurrencyMetadataRecord>))]
[JsonSerializable(typeof(List<CurrencyRateRecord>))]
[JsonSerializable(typeof(CurrencyRateSnapshot))]
internal sealed partial class CurrencyJsonContext : JsonSerializerContext;

public partial class CurrencyHttpClient : ICurrencyRateProvider
{
    private const string DefaultBaseCurrency = "USD";
    private readonly HttpClient _httpClient;
    private string _sourceCurrencyCode = DefaultBaseCurrency;

    public CurrencyHttpClient()
        : this(new HttpClient { BaseAddress = new Uri("https://api.frankfurter.dev/") })
    {
    }

    internal CurrencyHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public void Initialize(string sourceCurrencyCode, string responseLanguage)
    {
        _sourceCurrencyCode = string.IsNullOrWhiteSpace(sourceCurrencyCode)
            ? DefaultBaseCurrency
            : sourceCurrencyCode.ToUpperInvariant();
        _ = responseLanguage;
    }

    public async Task<CurrencyRateSnapshot> GetLatestRatesAsync(
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage metadataResponse = await _httpClient.GetAsync(
            "v2/currencies",
            cancellationToken);
        metadataResponse.EnsureSuccessStatusCode();

        using HttpResponseMessage ratesResponse = await _httpClient.GetAsync(
            $"v2/rates?base={Uri.EscapeDataString(_sourceCurrencyCode)}",
            cancellationToken);
        ratesResponse.EnsureSuccessStatusCode();

        await using Stream metadataStream = await metadataResponse.Content
            .ReadAsStreamAsync(cancellationToken);
        List<CurrencyMetadataRecord> currencies =
            await System.Text.Json.JsonSerializer.DeserializeAsync(
                metadataStream,
                CurrencyJsonContext.Default.ListCurrencyMetadataRecord,
                cancellationToken) ?? [];

        await using Stream ratesStream = await ratesResponse.Content
            .ReadAsStreamAsync(cancellationToken);
        List<CurrencyRateRecord> rates =
            await System.Text.Json.JsonSerializer.DeserializeAsync(
                ratesStream,
                CurrencyJsonContext.Default.ListCurrencyRateRecord,
                cancellationToken) ?? [];

        if (currencies.Count == 0 || rates.Count == 0)
        {
            throw new InvalidDataException("Frankfurter returned an empty currency response.");
        }

        string latestDate = rates.Max(rate => rate.Date) ?? string.Empty;
        rates.RemoveAll(rate => rate.QuoteCurrency.Equals(
            _sourceCurrencyCode,
            StringComparison.OrdinalIgnoreCase));
        rates.Add(new CurrencyRateRecord
        {
            // A base-to-base rate is always exactly one. Frankfurter v2
            // currently includes this row, while older responses did not, so
            // replace any returned row instead of appending a duplicate.
            Date = latestDate,
            BaseCurrency = _sourceCurrencyCode,
            QuoteCurrency = _sourceCurrencyCode,
            Rate = 1m
        });

        return new CurrencyRateSnapshot
        {
            FetchedAtUtc = DateTimeOffset.UtcNow,
            BaseCurrency = _sourceCurrencyCode,
            Currencies = currencies,
            Rates = rates
        };
    }
}

/// <summary>
/// CLDR 48.2 boundary. Frankfurter metadata is used as the ISO fallback while
/// the vendored tables supply localized names and symbols for every shipped UI
/// culture.
/// </summary>
public sealed class CldrCurrencyNameProvider : ICurrencyNameProvider
{
    private readonly string _cultureName;

    public CldrCurrencyNameProvider()
        : this(CultureInfo.CurrentUICulture.Name)
    {
    }

    internal CldrCurrencyNameProvider(string cultureName)
    {
        try
        {
            _cultureName = CultureInfo.GetCultureInfo(cultureName).Name;
        }
        catch (CultureNotFoundException)
        {
            _cultureName = string.Empty;
        }
    }

    public CurrencyDisplayMetadata GetCurrency(
        string isoCode,
        string fallbackName,
        string fallbackSymbol)
    {
        int fractionDigits = GetFractionDigits(isoCode);
        if (CldrCurrencyData.TryGet(
                _cultureName,
                isoCode,
                out CldrCurrencyDisplayData metadata))
        {
            // CLDR commonly uses the ISO code when base English has no distinct
            // symbol. Retain the provider's useful native symbol only when the
            // selected locale did not explicitly choose that ISO-code symbol.
            string symbol = metadata.Symbol.Equals(isoCode, StringComparison.OrdinalIgnoreCase) &&
                            !metadata.HasLocalizedSymbol &&
                            !string.IsNullOrWhiteSpace(fallbackSymbol)
                ? fallbackSymbol
                : metadata.Symbol;
            return new CurrencyDisplayMetadata(metadata.Name, symbol, fractionDigits);
        }

        return new CurrencyDisplayMetadata(
            string.IsNullOrWhiteSpace(fallbackName) ? isoCode : fallbackName,
            string.IsNullOrWhiteSpace(fallbackSymbol) ? isoCode : fallbackSymbol,
            fractionDigits);
    }

    public static int GetFractionDigits(string isoCode) =>
        CldrCurrencyData.GetFractionDigits(isoCode);
}
