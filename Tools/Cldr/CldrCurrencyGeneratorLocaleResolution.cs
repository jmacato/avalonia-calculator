// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.

internal sealed record CldrCurrencyGeneratorLocaleResolution(IReadOnlyList<CldrCurrencyGeneratorLocaleDefinition> Locales, SortedDictionary<string, CldrCurrencyGeneratorLocaleSource> Sources);
