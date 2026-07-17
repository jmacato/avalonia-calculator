// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal sealed record CldrCurrencyGeneratorLocaleIdParts(string Language, string? Script, string? Region)
{
    public override string ToString() => string.Join('_', new[] { Language, Script, Region }.Where(part => part is not null));
}
