// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using CalcEngine;
using UnitConversionManager;

namespace UnitConversionManager;

public class CurrencyStaticData
{
    public wstring CountryCode { get; set; } = "";

    public wstring CountryName { get; set; } = "";

    public wstring CurrencyCode { get; set; } = "";

    public wstring CurrencyName { get; set; } = "";

    public wstring CurrencySymbol { get; set; } = "";
}
