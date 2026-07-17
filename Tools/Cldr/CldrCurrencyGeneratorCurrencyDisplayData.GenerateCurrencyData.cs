// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

internal sealed record CldrCurrencyGeneratorCurrencyDisplayData(string Name, string Symbol, bool HasLocalizedSymbol);
