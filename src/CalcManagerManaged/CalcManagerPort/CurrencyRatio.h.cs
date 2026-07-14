// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using CalcEngine;
using UnitConversionManager;

namespace UnitConversionManager;

public class CurrencyRatio
{
    public double Ratio { get; set; }

    public wstring SourceCurrencyCode { get; set; }

    public wstring TargetCurrencyCode { get; set; }

    public CurrencyRatio(double ratio, string sourceCurrencyCode, string targetCurrencyCode)
    {
        this.Ratio = ratio;
        this.SourceCurrencyCode = sourceCurrencyCode;
        this.TargetCurrencyCode = targetCurrencyCode;
    }
}
