// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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

public sealed record CurrencyDisplayMetadata(string Name, string Symbol);

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
    public double Rate { get; set; }
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

        rates.Add(new CurrencyRateRecord
        {
            Date = rates.Max(rate => rate.Date) ?? string.Empty,
            BaseCurrency = _sourceCurrencyCode,
            QuoteCurrency = _sourceCurrencyCode,
            Rate = 1d
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
/// the vendored table supplies the symbols/names that differ for common UI
/// locales. Additional locale tables can be added without changing the loader.
/// </summary>
public sealed class CldrCurrencyNameProvider : ICurrencyNameProvider
{
    // The base English CLDR values cover the currencies selected by default on
    // the supported test platforms. An ISO fallback is required by the plan.
    private static readonly IReadOnlyDictionary<string, CurrencyDisplayMetadata> English =
        new Dictionary<string, CurrencyDisplayMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["AUD"] = new("Australian Dollar", "A$"),
            ["CAD"] = new("Canadian Dollar", "CA$"),
            ["CHF"] = new("Swiss Franc", "CHF"),
            ["CNY"] = new("Chinese Yuan", "CN¥"),
            ["EUR"] = new("Euro", "€"),
            ["GBP"] = new("British Pound", "£"),
            ["INR"] = new("Indian Rupee", "₹"),
            ["JPY"] = new("Japanese Yen", "¥"),
            ["KRW"] = new("South Korean Won", "₩"),
            ["PHP"] = new("Philippine Peso", "₱"),
            ["USD"] = new("US Dollar", "$"),
        };

    public CurrencyDisplayMetadata GetCurrency(
        string isoCode,
        string fallbackName,
        string fallbackSymbol)
    {
        if (English.TryGetValue(isoCode, out CurrencyDisplayMetadata? metadata))
        {
            return metadata;
        }

        return new CurrencyDisplayMetadata(
            string.IsNullOrWhiteSpace(fallbackName) ? isoCode : fallbackName,
            string.IsNullOrWhiteSpace(fallbackSymbol) ? isoCode : fallbackSymbol);
    }
}
