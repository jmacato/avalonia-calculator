// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Immutable;

namespace CalculatorApp.ViewModel.DataLoaders;

public partial class CurrencyHttpClient : ICurrencyRateProvider
{
    private const string DefaultBaseCurrency = "USD";
    private readonly HttpClient _httpClient;
    private string _sourceCurrencyCode = DefaultBaseCurrency;
    public CurrencyHttpClient() : this(new HttpClient { BaseAddress = new Uri("https://api.frankfurter.dev/") })
    {
    }

    internal CurrencyHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public void Initialize(string sourceCurrencyCode, string responseLanguage)
    {
        _sourceCurrencyCode = string.IsNullOrWhiteSpace(sourceCurrencyCode) ? DefaultBaseCurrency : sourceCurrencyCode.ToUpperInvariant();
        _ = responseLanguage;
    }

    public async Task<CurrencyRateSnapshot> GetLatestRatesAsync(CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage metadataResponse = await _httpClient.GetAsync(new Uri("v2/currencies", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        metadataResponse.EnsureSuccessStatusCode();
        using HttpResponseMessage ratesResponse = await _httpClient.GetAsync(new Uri($"v2/rates?base={Uri.EscapeDataString(_sourceCurrencyCode)}", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        ratesResponse.EnsureSuccessStatusCode();
        Stream metadataStream = await metadataResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        List<CurrencyMetadataRecord> currencies;
        await using (metadataStream.ConfigureAwait(false))
        {
            currencies = await System.Text.Json.JsonSerializer.DeserializeAsync(metadataStream, CurrencyJsonContext.Default.ListCurrencyMetadataRecord, cancellationToken).ConfigureAwait(false) ?? [];
        }

        Stream ratesStream = await ratesResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        List<CurrencyRateRecord> rates;
        await using (ratesStream.ConfigureAwait(false))
        {
            rates = await System.Text.Json.JsonSerializer.DeserializeAsync(ratesStream, CurrencyJsonContext.Default.ListCurrencyRateRecord, cancellationToken).ConfigureAwait(false) ?? [];
        }
        if (currencies.Count == 0 || rates.Count == 0)
        {
            throw new InvalidDataException("Frankfurter returned an empty currency response.");
        }

        string latestDate = rates.Max(rate => rate.Date) ?? string.Empty;
        rates.RemoveAll(rate => rate.QuoteCurrency.Equals(_sourceCurrencyCode, StringComparison.OrdinalIgnoreCase));
        rates.Add(new CurrencyRateRecord
        { // A base-to-base rate is always exactly one. Frankfurter v2
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
            Currencies = currencies.ToImmutableArray(),
            Rates = rates.ToImmutableArray()
        };
    }
}
