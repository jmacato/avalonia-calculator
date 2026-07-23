// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UnitConversionManager;

public sealed class CurrencyRatio(double ratio, string sourceCurrencyCode, string targetCurrencyCode)
{
    public double Ratio { get; set; } = ratio;

    public wstring SourceCurrencyCode { get; set; } = sourceCurrencyCode;

    public wstring TargetCurrencyCode { get; set; } = targetCurrencyCode;
}
