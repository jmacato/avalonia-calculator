// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.

internal sealed record CldrCurrencyGeneratorLocaleIdParts(string Language, string? Script, string? Region)
{
    public override string ToString()
    {
        return string.Join('_', new[] { Language, Script, Region }.Where(part => part is not null));
    }
}
