// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.

internal sealed record CldrCurrencyGeneratorLocaleSource(string CldrLocaleId, string Url, string Sha256, SortedDictionary<string, CldrCurrencyGeneratorCurrencyDisplayPatch> Currencies, SortedDictionary<string, string> UnitPatterns);
