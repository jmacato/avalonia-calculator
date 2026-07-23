// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Globalization;

namespace CalculatorApp.ViewModel.DataLoaders;
/// <summary>
/// CLDR 48.2 boundary. Frankfurter metadata is used as the ISO fallback while
/// the vendored tables supply localized names and symbols for every shipped UI
/// culture.
/// </summary>
public sealed class CldrCurrencyNameProvider : ICurrencyNameProvider
{
    private readonly string _cultureName;
    public CldrCurrencyNameProvider() : this(CultureInfo.CurrentUICulture.Name)
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

    public CurrencyDisplayMetadata GetCurrency(string isoCode, string fallbackName, string fallbackSymbol)
    {
        int fractionDigits = GetFractionDigits(isoCode);
        if (CldrCurrencyData.TryGet(_cultureName, isoCode, out CldrCurrencyDisplayData metadata))
        {
            // CLDR commonly uses the ISO code when base English has no distinct
            // symbol. Retain the provider's useful native symbol only when the
            // selected locale did not explicitly choose that ISO-code symbol.
            string symbol = metadata.Symbol.Equals(isoCode, StringComparison.OrdinalIgnoreCase) && !metadata.HasLocalizedSymbol && !string.IsNullOrWhiteSpace(fallbackSymbol) ? fallbackSymbol : metadata.Symbol;
            return new CurrencyDisplayMetadata(metadata.Name, symbol, fractionDigits);
        }

        return new CurrencyDisplayMetadata(string.IsNullOrWhiteSpace(fallbackName) ? isoCode : fallbackName, string.IsNullOrWhiteSpace(fallbackSymbol) ? isoCode : fallbackSymbol, fractionDigits);
    }

    public static int GetFractionDigits(string isoCode)
    {
        return CldrCurrencyData.GetFractionDigits(isoCode);
    }
}
